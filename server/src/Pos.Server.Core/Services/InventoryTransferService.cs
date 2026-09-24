namespace Pos.Server.Services;

using Pos.Domain.Logic;
using Pos.Server.Accessors;
using Pos.Server.Models;
using Pos.Server.Models.Entity;
using Pos.Server.Models.Parameters;
using Pos.Server.Models.Views;

public enum InventoryTransferResultStatus
{
    Success,
    NotFound,
    Violation
}

public sealed record InventoryTransferResult(InventoryTransferResultStatus Status, InventoryTransferDetailView? Detail = null, RuleError? Violation = null);

// 店舗間移動: 依頼を登録し、出荷で出荷店の在庫を減らし (TransferOut)、受領で入荷店の在庫を増やす (TransferIn)。
// 出荷とキャンセルは依頼のとき、受領は出荷済みのときだけ
public sealed class InventoryTransferService
{
    private readonly TimeProvider timeProvider;
    private readonly IDbProvider provider;
    private readonly MasterAccessor masterAccessor;
    private readonly ProductAccessor productAccessor;
    private readonly InventoryAccessor inventoryAccessor;
    private readonly InventoryTransferAccessor transferAccessor;
    private readonly ChangeNotificationService changeNotification;

    public InventoryTransferService(
        TimeProvider timeProvider,
        IDbProvider provider,
        MasterAccessor masterAccessor,
        ProductAccessor productAccessor,
        InventoryAccessor inventoryAccessor,
        InventoryTransferAccessor transferAccessor,
        ChangeNotificationService changeNotification)
    {
        this.timeProvider = timeProvider;
        this.provider = provider;
        this.masterAccessor = masterAccessor;
        this.productAccessor = productAccessor;
        this.inventoryAccessor = inventoryAccessor;
        this.transferAccessor = transferAccessor;
        this.changeNotification = changeNotification;
    }

    //--------------------------------------------------------------------------------
    // Query
    //--------------------------------------------------------------------------------

    public async ValueTask<PagedResult<InventoryTransferDetailView>> QueryPageAsync(InventoryTransferQueryParameter parameter, CancellationToken cancellationToken)
    {
        var total = await transferAccessor.CountAsync(parameter.StoreId, parameter.FromStoreId, parameter.ToStoreId, parameter.Status, parameter.OpenOnly, cancellationToken);
        var items = await transferAccessor.QueryListAsync(parameter.StoreId, parameter.FromStoreId, parameter.ToStoreId, parameter.Status, parameter.OpenOnly, parameter.Sort, parameter.Desc, parameter.Size, parameter.Page * parameter.Size, cancellationToken);
        var stores = await QueryStoreNamesAsync(cancellationToken);
        var details = new List<InventoryTransferDetailView>(items.Count);
        foreach (var item in items)
        {
            details.Add(await LoadDetailAsync(item, stores, cancellationToken));
        }

        return new PagedResult<InventoryTransferDetailView>((int)total, parameter.Page, parameter.Size, details);
    }

    public async ValueTask<InventoryTransferDetailView?> QueryDetailAsync(Guid id, CancellationToken cancellationToken)
    {
        var transfer = await transferAccessor.QueryAsync(id, cancellationToken);
        return transfer is null ? null : await LoadDetailAsync(transfer, await QueryStoreNamesAsync(cancellationToken), cancellationToken);
    }

    // 未受領の件数 (ダッシュボード)。storeId は出荷店か入荷店のどちらか
    public async ValueTask<(int Requested, int Shipped)> CountOpenAsync(Guid? storeId, CancellationToken cancellationToken)
    {
        var requested = await transferAccessor.CountAsync(storeId, null, null, InventoryTransferStatus.Requested, false, cancellationToken);
        var shipped = await transferAccessor.CountAsync(storeId, null, null, InventoryTransferStatus.Shipped, false, cancellationToken);
        return ((int)requested, (int)shipped);
    }

    //--------------------------------------------------------------------------------
    // Create
    //--------------------------------------------------------------------------------

    // 依頼の登録。移動番号は出荷店ごとに採番し、明細の商品コード・名称は登録時点の写しを持つ
    public async ValueTask<InventoryTransferResult> CreateAsync(Guid fromStoreId, Guid toStoreId, string? note, IReadOnlyList<InventoryTransferLineEntity> lines, CancellationToken cancellationToken)
    {
        var fromStore = await masterAccessor.QueryStoreAsync(fromStoreId, cancellationToken);
        var toStore = await masterAccessor.QueryStoreAsync(toStoreId, cancellationToken);
        if ((fromStore is not { IsDeleted: false }) || (toStore is not { IsDeleted: false }))
        {
            return Violated(ErrorCode.ValidationError, RuleReason.StoreNotFound);
        }

        var products = (await productAccessor.QueryListByIdsAsync(lines.Select(static x => x.ProductId).Distinct(), cancellationToken)).ToDictionary(static x => x.Id);
        if (lines.Any(x => !products.ContainsKey(x.ProductId)))
        {
            return Violated(ErrorCode.ProductNotFound, RuleReason.ProductNotFound);
        }

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var id = Guid.CreateVersion7();
        var inserted = await provider.UsingTxAsync(async (_, tx) =>
        {
            var entity = await transferAccessor.InsertAsync(tx, id, fromStoreId, fromStore.Code, toStoreId, note, now, cancellationToken);
            for (var i = 0; i < lines.Count; i++)
            {
                var line = lines[i];
                var product = products[line.ProductId];
                line.Id = Guid.CreateVersion7();
                line.TransferId = id;
                line.LineNo = i + 1;
                line.ProductCode = product.Code;
                line.ProductName = product.Name;
                line.ReceivedQuantity = null;
                await transferAccessor.InsertLineAsync(tx, line, cancellationToken);
            }

            await tx.CommitAsync(cancellationToken);
            return entity!;
        }, cancellationToken);
        changeNotification.Notify(DataChangeKind.Inventory);
        return new InventoryTransferResult(InventoryTransferResultStatus.Success, new InventoryTransferDetailView { Transfer = inserted, FromStoreName = fromStore.Name, ToStoreName = toStore.Name, Lines = lines });
    }

    //--------------------------------------------------------------------------------
    // Ship / Receive / Cancel
    //--------------------------------------------------------------------------------

    // 出荷。依頼の数で出荷店の在庫を減らす
    public async ValueTask<InventoryTransferResult> ShipAsync(Guid id, Guid? staffId, DateTime? shippedAt, CancellationToken cancellationToken)
    {
        var lines = await transferAccessor.QueryLineListAsync(id, cancellationToken);
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var occurredAt = shippedAt ?? now;
        var shipped = await provider.UsingTxAsync(async (_, tx) =>
        {
            var updated = await transferAccessor.UpdateShippedAsync(tx, id, occurredAt, staffId, now, cancellationToken);
            if (updated is null)
            {
                return null;
            }

            foreach (var line in lines)
            {
                await AddChangeAsync(tx, updated, line, updated.FromStoreId, InventoryChangeType.TransferOut, -line.Quantity, staffId, occurredAt, now, cancellationToken);
            }

            await tx.CommitAsync(cancellationToken);
            return updated;
        }, cancellationToken);
        if (shipped is null)
        {
            return await ResolveFailureAsync(id, static x => InventoryMovementLogic.ValidateTransferShip(x.Status), cancellationToken);
        }

        changeNotification.Notify(DataChangeKind.Inventory);
        return new InventoryTransferResult(InventoryTransferResultStatus.Success, ToDetail(shipped, lines, await QueryStoreNamesAsync(cancellationToken)));
    }

    // 受領。quantities にない明細は出荷した数で受け取る。数が 0 の明細は在庫を動かさない (出荷との差は移動の記録に残す)
    public async ValueTask<InventoryTransferResult> ReceiveAsync(Guid id, Guid? staffId, DateTime? receivedAt, IReadOnlyDictionary<Guid, decimal> quantities, CancellationToken cancellationToken)
    {
        var lines = await transferAccessor.QueryLineListAsync(id, cancellationToken);
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var occurredAt = receivedAt ?? now;
        var received = await provider.UsingTxAsync(async (_, tx) =>
        {
            var updated = await transferAccessor.UpdateReceivedAsync(tx, id, occurredAt, staffId, now, cancellationToken);
            if (updated is null)
            {
                return null;
            }

            foreach (var line in lines)
            {
                var quantity = quantities.GetValueOrDefault(line.Id, line.Quantity);
                line.ReceivedQuantity = quantity;
                await transferAccessor.UpdateLineReceivedAsync(tx, line.Id, quantity, cancellationToken);
                if (quantity != 0m)
                {
                    await AddChangeAsync(tx, updated, line, updated.ToStoreId, InventoryChangeType.TransferIn, quantity, staffId, occurredAt, now, cancellationToken);
                }
            }

            await tx.CommitAsync(cancellationToken);
            return updated;
        }, cancellationToken);
        if (received is null)
        {
            return await ResolveFailureAsync(id, static x => InventoryMovementLogic.ValidateTransferReceive(x.Status), cancellationToken);
        }

        changeNotification.Notify(DataChangeKind.Inventory);
        return new InventoryTransferResult(InventoryTransferResultStatus.Success, ToDetail(received, lines, await QueryStoreNamesAsync(cancellationToken)));
    }

    public async ValueTask<InventoryTransferResult> CancelAsync(Guid id, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var cancelled = await transferAccessor.UpdateCancelledAsync(id, now, now, cancellationToken);
        if (cancelled is null)
        {
            return await ResolveFailureAsync(id, static x => InventoryMovementLogic.ValidateTransferCancel(x.Status), cancellationToken);
        }

        changeNotification.Notify(DataChangeKind.Inventory);
        return new InventoryTransferResult(InventoryTransferResultStatus.Success, await LoadDetailAsync(cancelled, await QueryStoreNamesAsync(cancellationToken), cancellationToken));
    }

    //--------------------------------------------------------------------------------
    // Helper
    //--------------------------------------------------------------------------------

    private static InventoryTransferResult Violated(ErrorCode code, RuleReason reason) =>
        new(InventoryTransferResultStatus.Violation, Violation: new RuleError(code, reason));

    // 在庫を加減算し、移動番号を理由にして変動を残す
    private async ValueTask AddChangeAsync(DbTransaction tx, InventoryTransferEntity transfer, InventoryTransferLineEntity line, Guid storeId, InventoryChangeType type, decimal delta, Guid? staffId, DateTime occurredAt, DateTime now, CancellationToken cancellationToken)
    {
        var after = await inventoryAccessor.AddQuantityAsync(tx, storeId, line.ProductId, delta, now, cancellationToken);
        await inventoryAccessor.InsertChangeAsync(tx, new InventoryChangeEntity
        {
            Id = Guid.CreateVersion7(),
            StoreId = storeId,
            ProductId = line.ProductId,
            Type = type,
            QuantityDelta = delta,
            QuantityAfter = after,
            Reason = transfer.TransferNo,
            ReferenceType = InventoryReferenceType.InventoryTransfer,
            ReferenceId = transfer.Id,
            ReferenceLineId = line.Id,
            StaffId = staffId,
            OccurredAt = occurredAt,
            CreatedAt = now
        }, cancellationToken);
    }

    // 状態を条件にした更新で行が返らなかった理由 (ない、または状態が合わない)
    private async ValueTask<InventoryTransferResult> ResolveFailureAsync(Guid id, Func<InventoryTransferEntity, RuleError?> validate, CancellationToken cancellationToken)
    {
        var current = await transferAccessor.QueryAsync(id, cancellationToken);
        return current is null
            ? new InventoryTransferResult(InventoryTransferResultStatus.NotFound)
            : new InventoryTransferResult(InventoryTransferResultStatus.Violation, Violation: validate(current) ?? new RuleError(ErrorCode.InventoryTransferStatusInvalid, RuleReason.InventoryTransferNotRequested));
    }

    // 店舗は削除済みも名前を引く
    private async ValueTask<Dictionary<Guid, string>> QueryStoreNamesAsync(CancellationToken cancellationToken) =>
        (await masterAccessor.QueryStoreAllAsync(true, cancellationToken)).ToDictionary(static x => x.Id, static x => x.Name);

    private static InventoryTransferDetailView ToDetail(InventoryTransferEntity transfer, IReadOnlyList<InventoryTransferLineEntity> lines, Dictionary<Guid, string> stores) =>
        new()
        {
            Transfer = transfer,
            FromStoreName = stores.GetValueOrDefault(transfer.FromStoreId, String.Empty),
            ToStoreName = stores.GetValueOrDefault(transfer.ToStoreId, String.Empty),
            Lines = lines
        };

    private async ValueTask<InventoryTransferDetailView> LoadDetailAsync(InventoryTransferEntity transfer, Dictionary<Guid, string> stores, CancellationToken cancellationToken) =>
        ToDetail(transfer, await transferAccessor.QueryLineListAsync(transfer.Id, cancellationToken), stores);
}
