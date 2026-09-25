namespace Pos.Server.Services;

using Pos.Domain.Logic;
using Pos.Server.Accessors;
using Pos.Server.Models;
using Pos.Server.Models.Entity;
using Pos.Server.Models.Parameters;
using Pos.Server.Models.Views;

public enum InventoryReceiptResultStatus
{
    Success,
    NotFound,
    Violation
}

public sealed record InventoryReceiptResult(InventoryReceiptResultStatus Status, InventoryReceiptDetailView? Detail = null, RuleError? Violation = null);

// 入荷: 入荷予定を登録し、受領で入荷先の店舗の在庫を増やす (変動は Receive)。受領とキャンセルは入荷予定のときだけ。
// 発注から作った入荷予定は、受領とキャンセルで発注も入荷済み・キャンセルにする
public sealed class InventoryReceiptService
{
    private readonly TimeProvider timeProvider;
    private readonly IDbProvider provider;
    private readonly MasterAccessor masterAccessor;
    private readonly ProductAccessor productAccessor;
    private readonly InventoryAccessor inventoryAccessor;
    private readonly InventoryReceiptAccessor receiptAccessor;
    private readonly PurchaseOrderAccessor purchaseOrderAccessor;
    private readonly ChangeNotificationService changeNotification;

    public InventoryReceiptService(
        TimeProvider timeProvider,
        IDbProvider provider,
        MasterAccessor masterAccessor,
        ProductAccessor productAccessor,
        InventoryAccessor inventoryAccessor,
        InventoryReceiptAccessor receiptAccessor,
        PurchaseOrderAccessor purchaseOrderAccessor,
        ChangeNotificationService changeNotification)
    {
        this.timeProvider = timeProvider;
        this.provider = provider;
        this.masterAccessor = masterAccessor;
        this.productAccessor = productAccessor;
        this.inventoryAccessor = inventoryAccessor;
        this.receiptAccessor = receiptAccessor;
        this.purchaseOrderAccessor = purchaseOrderAccessor;
        this.changeNotification = changeNotification;
    }

    //--------------------------------------------------------------------------------
    // Query
    //--------------------------------------------------------------------------------

    // 明細と仕入先の名前付き (仕入先は削除済みも名前を引く)
    public async ValueTask<PagedResult<InventoryReceiptDetailView>> QueryPageAsync(InventoryReceiptQueryParameter parameter, CancellationToken cancellationToken)
    {
        var total = await receiptAccessor.CountAsync(parameter.StoreId, parameter.SupplierId, parameter.Status, parameter.From, parameter.To, cancellationToken);
        var items = await receiptAccessor.QueryListAsync(parameter.StoreId, parameter.SupplierId, parameter.Status, parameter.From, parameter.To, parameter.Sort, parameter.Desc, parameter.Size, parameter.Page * parameter.Size, cancellationToken);
        var suppliers = await QuerySupplierNamesAsync(cancellationToken);
        var details = new List<InventoryReceiptDetailView>(items.Count);
        foreach (var item in items)
        {
            details.Add(await LoadDetailAsync(item, suppliers, cancellationToken));
        }

        return new PagedResult<InventoryReceiptDetailView>((int)total, parameter.Page, parameter.Size, details);
    }

    public async ValueTask<InventoryReceiptDetailView?> QueryDetailAsync(Guid id, CancellationToken cancellationToken)
    {
        var receipt = await receiptAccessor.QueryAsync(id, cancellationToken);
        return receipt is null ? null : await LoadDetailAsync(receipt, await QuerySupplierNamesAsync(cancellationToken), cancellationToken);
    }

    //--------------------------------------------------------------------------------
    // Create
    //--------------------------------------------------------------------------------

    // 入荷予定の登録。明細の商品コード・名称は登録時点の写しを持つ
    public async ValueTask<InventoryReceiptResult> CreateAsync(InventoryReceiptEntity receipt, IReadOnlyList<InventoryReceiptLineEntity> lines, CancellationToken cancellationToken)
    {
        if (await masterAccessor.QueryStoreAsync(receipt.StoreId, cancellationToken) is not { IsDeleted: false })
        {
            return Violated(ErrorCode.ValidationError, RuleReason.StoreNotFound);
        }

        var supplier = await masterAccessor.QuerySupplierAsync(receipt.SupplierId, cancellationToken);
        if (supplier is not { IsDeleted: false })
        {
            return Violated(ErrorCode.ValidationError, RuleReason.SupplierNotFound);
        }

        var products = (await productAccessor.QueryListByIdsAsync(lines.Select(static x => x.ProductId).Distinct(), cancellationToken)).ToDictionary(static x => x.Id);
        if (lines.Any(x => !products.ContainsKey(x.ProductId)))
        {
            return Violated(ErrorCode.ProductNotFound, RuleReason.ProductNotFound);
        }

        var now = timeProvider.GetUtcNow().UtcDateTime;
        receipt.Id = Guid.CreateVersion7();
        receipt.Status = InventoryReceiptStatus.Draft;
        receipt.CreatedAt = now;
        receipt.UpdatedAt = now;
        receipt.Version = 1;
        for (var i = 0; i < lines.Count; i++)
        {
            var line = lines[i];
            var product = products[line.ProductId];
            line.Id = Guid.CreateVersion7();
            line.ReceiptId = receipt.Id;
            line.LineNo = i + 1;
            line.ProductCode = product.Code;
            line.ProductName = product.Name;
            line.ReceivedQuantity = null;
        }

        await provider.UsingTxAsync(async (_, tx) =>
        {
            await receiptAccessor.InsertAsync(tx, receipt, cancellationToken);
            foreach (var line in lines)
            {
                await receiptAccessor.InsertLineAsync(tx, line, cancellationToken);
            }

            await tx.CommitAsync(cancellationToken);
        }, cancellationToken);
        return new InventoryReceiptResult(InventoryReceiptResultStatus.Success, new InventoryReceiptDetailView { Receipt = receipt, SupplierName = supplier.Name, Lines = lines });
    }

    //--------------------------------------------------------------------------------
    // Receive / Cancel
    //--------------------------------------------------------------------------------

    // 受領。quantities にない明細は予定の数で受け取る。数が 0 の明細は在庫を動かさない
    public async ValueTask<InventoryReceiptResult> ReceiveAsync(Guid id, Guid? staffId, DateTime? receivedAt, IReadOnlyDictionary<Guid, decimal> quantities, CancellationToken cancellationToken)
    {
        var lines = await receiptAccessor.QueryLineListAsync(id, cancellationToken);
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var occurredAt = receivedAt ?? now;
        var received = await provider.UsingTxAsync(async (_, tx) =>
        {
            var updated = await receiptAccessor.UpdateReceivedAsync(tx, id, occurredAt, staffId, now, cancellationToken);
            if (updated is null)
            {
                return null;
            }

            foreach (var line in lines)
            {
                var quantity = quantities.GetValueOrDefault(line.Id, line.Quantity);
                line.ReceivedQuantity = quantity;
                await receiptAccessor.UpdateLineReceivedAsync(tx, line.Id, quantity, cancellationToken);
                if (quantity == 0m)
                {
                    continue;
                }

                var after = await inventoryAccessor.AddQuantityAsync(tx, updated.StoreId, line.ProductId, quantity, now, cancellationToken);
                await inventoryAccessor.InsertChangeAsync(tx, new InventoryChangeEntity
                {
                    Id = Guid.CreateVersion7(),
                    StoreId = updated.StoreId,
                    ProductId = line.ProductId,
                    Type = InventoryChangeType.Receive,
                    QuantityDelta = quantity,
                    QuantityAfter = after,
                    Reason = updated.SlipNo,
                    ReferenceType = InventoryReferenceType.InventoryReceipt,
                    ReferenceId = updated.Id,
                    ReferenceLineId = line.Id,
                    StaffId = staffId,
                    OccurredAt = occurredAt,
                    CreatedAt = now
                }, cancellationToken);
            }

            await purchaseOrderAccessor.UpdateReceivedByReceiptIdAsync(tx, updated.Id, now, cancellationToken);
            await tx.CommitAsync(cancellationToken);
            return updated;
        }, cancellationToken);
        if (received is null)
        {
            return await ResolveFailureAsync(id, static x => InventoryMovementLogic.ValidateReceiptReceive(x.Status), cancellationToken);
        }

        changeNotification.Notify(DataChangeKind.Inventory);
        return new InventoryReceiptResult(InventoryReceiptResultStatus.Success, new InventoryReceiptDetailView
        {
            Receipt = received,
            SupplierName = await QuerySupplierNameAsync(received.SupplierId, cancellationToken),
            Lines = lines,
            PurchaseOrder = await purchaseOrderAccessor.QueryByReceiptIdAsync(received.Id, cancellationToken)
        });
    }

    public async ValueTask<InventoryReceiptResult> CancelAsync(Guid id, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var cancelled = await provider.UsingTxAsync(async (_, tx) =>
        {
            var entity = await receiptAccessor.UpdateCancelledAsync(tx, id, now, now, cancellationToken);
            if (entity is null)
            {
                return null;
            }

            await purchaseOrderAccessor.UpdateCancelledByReceiptIdAsync(tx, entity.Id, now, now, cancellationToken);
            await tx.CommitAsync(cancellationToken);
            return entity;
        }, cancellationToken);
        if (cancelled is null)
        {
            return await ResolveFailureAsync(id, static x => InventoryMovementLogic.ValidateReceiptCancel(x.Status), cancellationToken);
        }

        return new InventoryReceiptResult(InventoryReceiptResultStatus.Success, await LoadDetailAsync(cancelled, await QuerySupplierNamesAsync(cancellationToken), cancellationToken));
    }

    //--------------------------------------------------------------------------------
    // Helper
    //--------------------------------------------------------------------------------

    private static InventoryReceiptResult Violated(ErrorCode code, RuleReason reason) =>
        new(InventoryReceiptResultStatus.Violation, Violation: new RuleError(code, reason));

    // 状態を条件にした更新で行が返らなかった理由 (ない、または状態が合わない)
    private async ValueTask<InventoryReceiptResult> ResolveFailureAsync(Guid id, Func<InventoryReceiptEntity, RuleError?> validate, CancellationToken cancellationToken)
    {
        var current = await receiptAccessor.QueryAsync(id, cancellationToken);
        return current is null
            ? new InventoryReceiptResult(InventoryReceiptResultStatus.NotFound)
            : new InventoryReceiptResult(InventoryReceiptResultStatus.Violation, Violation: validate(current) ?? new RuleError(ErrorCode.InventoryReceiptStatusInvalid, RuleReason.InventoryReceiptNotDraft));
    }

    private async ValueTask<Dictionary<Guid, string>> QuerySupplierNamesAsync(CancellationToken cancellationToken) =>
        (await masterAccessor.QuerySupplierListAsync(true, cancellationToken)).ToDictionary(static x => x.Id, static x => x.Name);

    private async ValueTask<string> QuerySupplierNameAsync(Guid id, CancellationToken cancellationToken) =>
        (await masterAccessor.QuerySupplierAsync(id, cancellationToken))?.Name ?? String.Empty;

    private async ValueTask<InventoryReceiptDetailView> LoadDetailAsync(InventoryReceiptEntity receipt, Dictionary<Guid, string> suppliers, CancellationToken cancellationToken) =>
        new()
        {
            Receipt = receipt,
            SupplierName = suppliers.GetValueOrDefault(receipt.SupplierId, String.Empty),
            Lines = await receiptAccessor.QueryLineListAsync(receipt.Id, cancellationToken),
            PurchaseOrder = await purchaseOrderAccessor.QueryByReceiptIdAsync(receipt.Id, cancellationToken)
        };
}
