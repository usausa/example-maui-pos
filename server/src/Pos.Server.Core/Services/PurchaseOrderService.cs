namespace Pos.Server.Services;

using Pos.Domain.Logic;
using Pos.Server.Accessors;
using Pos.Server.Models;
using Pos.Server.Models.Entity;
using Pos.Server.Models.Parameters;
using Pos.Server.Models.Views;

public enum PurchaseOrderResultStatus
{
    Success,
    NotFound,
    VersionMismatch,
    // 業務ルール違反 (Violation に理由)
    Violation
}

// 発注の登録・変更・状態遷移の結果
public sealed record PurchaseOrderResult(PurchaseOrderResultStatus Status, PurchaseOrderDetailView? Detail = null, RuleError? Violation = null);

// 発注 (仕入先への注文)。[発注] で明細を写した入荷予定を作る。入荷予定の受領とキャンセルで入荷済み・キャンセルになる (InventoryReceiptService)
public sealed class PurchaseOrderService
{
    private readonly TimeProvider timeProvider;
    private readonly IDbProvider provider;
    private readonly MasterAccessor masterAccessor;
    private readonly ProductAccessor productAccessor;
    private readonly PurchaseOrderAccessor purchaseOrderAccessor;
    private readonly InventoryReceiptAccessor receiptAccessor;

    public PurchaseOrderService(
        TimeProvider timeProvider,
        IDbProvider provider,
        MasterAccessor masterAccessor,
        ProductAccessor productAccessor,
        PurchaseOrderAccessor purchaseOrderAccessor,
        InventoryReceiptAccessor receiptAccessor)
    {
        this.timeProvider = timeProvider;
        this.provider = provider;
        this.masterAccessor = masterAccessor;
        this.productAccessor = productAccessor;
        this.purchaseOrderAccessor = purchaseOrderAccessor;
        this.receiptAccessor = receiptAccessor;
    }

    //--------------------------------------------------------------------------------
    // Query
    //--------------------------------------------------------------------------------

    // 明細と仕入先の名前付き (仕入先は削除済みも名前を引く)
    public async ValueTask<PagedResult<PurchaseOrderDetailView>> QueryPageAsync(PurchaseOrderQueryParameter parameter, CancellationToken cancellationToken)
    {
        var total = await purchaseOrderAccessor.CountAsync(parameter.StoreId, parameter.SupplierId, parameter.Status, parameter.OpenOnly, parameter.From, parameter.To, cancellationToken);
        var items = await purchaseOrderAccessor.QueryListAsync(parameter.StoreId, parameter.SupplierId, parameter.Status, parameter.OpenOnly, parameter.From, parameter.To, parameter.Sort, parameter.Desc, parameter.Size, parameter.Page * parameter.Size, cancellationToken);
        var suppliers = await QuerySupplierNamesAsync(cancellationToken);
        var details = new List<PurchaseOrderDetailView>(items.Count);
        foreach (var item in items)
        {
            details.Add(await LoadDetailAsync(item, suppliers, cancellationToken));
        }

        return new PagedResult<PurchaseOrderDetailView>((int)total, parameter.Page, parameter.Size, details);
    }

    public async ValueTask<PurchaseOrderDetailView?> QueryDetailAsync(Guid id, CancellationToken cancellationToken)
    {
        var order = await purchaseOrderAccessor.QueryAsync(id, cancellationToken);
        return order is null ? null : await LoadDetailAsync(order, await QuerySupplierNamesAsync(cancellationToken), cancellationToken);
    }

    // 発注書 PDF (発注元の会社と店舗、仕入先は削除済みも引く)
    public async ValueTask<PurchaseOrderReportView?> QueryReportAsync(Guid id, CancellationToken cancellationToken)
    {
        var detail = await QueryDetailAsync(id, cancellationToken);
        if (detail is null)
        {
            return null;
        }

        var order = detail.PurchaseOrder;
        var store = await masterAccessor.QueryStoreAsync(order.StoreId, cancellationToken);
        var settings = await masterAccessor.QuerySettingsAsync(cancellationToken);
        return new PurchaseOrderReportView
        {
            Detail = detail,
            CompanyName = settings?.CompanyName ?? String.Empty,
            Store = store,
            Supplier = await masterAccessor.QuerySupplierAsync(order.SupplierId, cancellationToken),
            TimeZone = StoreService.ResolveTimeZone(store?.TimeZone)
        };
    }

    //--------------------------------------------------------------------------------
    // Create / Update
    //--------------------------------------------------------------------------------

    // 下書きで登録する。明細の商品コード・名称は登録時点の写しを持つ
    public async ValueTask<PurchaseOrderResult> CreateAsync(PurchaseOrderEntity order, IReadOnlyList<PurchaseOrderLineEntity> lines, CancellationToken cancellationToken)
    {
        var store = await masterAccessor.QueryStoreAsync(order.StoreId, cancellationToken);
        if (store is not { IsDeleted: false })
        {
            return Violated(ErrorCode.ValidationError, RuleReason.StoreNotFound);
        }

        var supplier = await masterAccessor.QuerySupplierAsync(order.SupplierId, cancellationToken);
        if (supplier is not { IsDeleted: false })
        {
            return Violated(ErrorCode.ValidationError, RuleReason.SupplierNotFound);
        }

        var products = await QueryProductsAsync(lines, cancellationToken);
        if (products is null)
        {
            return Violated(ErrorCode.ProductNotFound, RuleReason.ProductNotFound);
        }

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var id = Guid.CreateVersion7();
        var copied = ToLines(id, lines, products);
        var inserted = await provider.UsingTxAsync(async (_, tx) =>
        {
            var entity = await purchaseOrderAccessor.InsertAsync(tx, id, store.Id, store.Code, supplier.Id, order.ExpectedDate, order.Note, now, cancellationToken);
            foreach (var line in copied)
            {
                await purchaseOrderAccessor.InsertLineAsync(tx, line, cancellationToken);
            }

            await tx.CommitAsync(cancellationToken);
            return entity!;
        }, cancellationToken);
        return new PurchaseOrderResult(PurchaseOrderResultStatus.Success, new PurchaseOrderDetailView { PurchaseOrder = inserted, SupplierName = supplier.Name, Lines = copied });
    }

    // 仕入先・希望納期・備考・明細の変更 (下書きのときだけ)。明細は全体を置き換える
    public async ValueTask<PurchaseOrderResult> UpdateAsync(Guid id, PurchaseOrderUpdateParameter parameter, CancellationToken cancellationToken)
    {
        var supplier = await masterAccessor.QuerySupplierAsync(parameter.SupplierId, cancellationToken);
        if (supplier is not { IsDeleted: false })
        {
            return Violated(ErrorCode.ValidationError, RuleReason.SupplierNotFound);
        }

        var products = await QueryProductsAsync(parameter.Lines, cancellationToken);
        if (products is null)
        {
            return Violated(ErrorCode.ProductNotFound, RuleReason.ProductNotFound);
        }

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var lines = ToLines(id, parameter.Lines, products);
        var updated = await provider.UsingTxAsync(async (_, tx) =>
        {
            var entity = await purchaseOrderAccessor.UpdateAsync(tx, id, parameter.SupplierId, parameter.ExpectedDate, parameter.Note, now, parameter.Version, cancellationToken);
            if (entity is null)
            {
                return null;
            }

            await purchaseOrderAccessor.DeleteLinesAsync(tx, id, cancellationToken);
            foreach (var line in lines)
            {
                await purchaseOrderAccessor.InsertLineAsync(tx, line, cancellationToken);
            }

            await tx.CommitAsync(cancellationToken);
            return entity;
        }, cancellationToken);

        if (updated is not null)
        {
            return new PurchaseOrderResult(PurchaseOrderResultStatus.Success, new PurchaseOrderDetailView { PurchaseOrder = updated, SupplierName = supplier.Name, Lines = lines });
        }

        // 行が返らない理由: ない、下書きでない、版の不一致
        var current = await purchaseOrderAccessor.QueryAsync(id, cancellationToken);
        if (current is null)
        {
            return new PurchaseOrderResult(PurchaseOrderResultStatus.NotFound);
        }

        return PurchaseOrderLogic.ValidateUpdate(current.Status) is { } error
            ? new PurchaseOrderResult(PurchaseOrderResultStatus.Violation, Violation: error)
            : new PurchaseOrderResult(PurchaseOrderResultStatus.VersionMismatch);
    }

    //--------------------------------------------------------------------------------
    // Order / Cancel
    //--------------------------------------------------------------------------------

    // 発注: 明細を写した入荷予定を作る (入荷予定日は希望納期)。下書きのときだけ
    public async ValueTask<PurchaseOrderResult> OrderAsync(Guid id, string? orderedBy, CancellationToken cancellationToken)
    {
        var order = await purchaseOrderAccessor.QueryAsync(id, cancellationToken);
        if (order is null)
        {
            return new PurchaseOrderResult(PurchaseOrderResultStatus.NotFound);
        }

        if (PurchaseOrderLogic.ValidateOrder(order.Status) is { } error)
        {
            return new PurchaseOrderResult(PurchaseOrderResultStatus.Violation, Violation: error);
        }

        var supplier = await masterAccessor.QuerySupplierAsync(order.SupplierId, cancellationToken);
        if (supplier is not { IsDeleted: false })
        {
            return Violated(ErrorCode.ValidationError, RuleReason.SupplierNotFound);
        }

        var lines = await purchaseOrderAccessor.QueryLineListAsync(id, cancellationToken);
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var receipt = new InventoryReceiptEntity
        {
            Id = Guid.CreateVersion7(),
            StoreId = order.StoreId,
            SupplierId = order.SupplierId,
            ExpectedDate = order.ExpectedDate,
            Status = InventoryReceiptStatus.Draft,
            Note = order.Note,
            CreatedAt = now,
            UpdatedAt = now,
            Version = 1
        };
        var ordered = await provider.UsingTxAsync(async (_, tx) =>
        {
            // 発注は入荷予定を参照するので、入荷予定を先に作る
            await receiptAccessor.InsertAsync(tx, receipt, cancellationToken);
            foreach (var line in lines)
            {
                await receiptAccessor.InsertLineAsync(tx, ToReceiptLine(receipt.Id, line), cancellationToken);
            }

            // 読んでから更新するまでに発注かキャンセルが先に通っていたら、コミットせずに入荷予定ごと取り消す
            var entity = await purchaseOrderAccessor.UpdateOrderedAsync(tx, id, now, orderedBy, receipt.Id, now, cancellationToken);
            if (entity is null)
            {
                return null;
            }

            await tx.CommitAsync(cancellationToken);
            return entity;
        }, cancellationToken);

        return ordered is null
            ? await ResolveFailureAsync(id, static x => PurchaseOrderLogic.ValidateOrder(x.Status), cancellationToken)
            : new PurchaseOrderResult(PurchaseOrderResultStatus.Success, new PurchaseOrderDetailView { PurchaseOrder = ordered, SupplierName = supplier.Name, Lines = lines });
    }

    // キャンセル (下書きか発注済み)。発注済みは作った入荷予定もキャンセルする
    public async ValueTask<PurchaseOrderResult> CancelAsync(Guid id, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var cancelled = await provider.UsingTxAsync(async (_, tx) =>
        {
            var entity = await purchaseOrderAccessor.UpdateCancelledAsync(tx, id, now, now, cancellationToken);
            if (entity is null)
            {
                return null;
            }

            // 入荷予定が先に受領されていたら、発注もキャンセルしない (受領で入荷済みになる)
            if ((entity.ReceiptId is { } receiptId) && (await receiptAccessor.UpdateCancelledAsync(tx, receiptId, now, now, cancellationToken) is null))
            {
                return null;
            }

            await tx.CommitAsync(cancellationToken);
            return entity;
        }, cancellationToken);

        return cancelled is null
            ? await ResolveFailureAsync(id, static x => PurchaseOrderLogic.ValidateCancel(x.Status), cancellationToken)
            : new PurchaseOrderResult(PurchaseOrderResultStatus.Success, await LoadDetailAsync(cancelled, await QuerySupplierNamesAsync(cancellationToken), cancellationToken));
    }

    //--------------------------------------------------------------------------------
    // Helper
    //--------------------------------------------------------------------------------

    private static PurchaseOrderResult Violated(ErrorCode code, RuleReason reason) =>
        new(PurchaseOrderResultStatus.Violation, Violation: new RuleError(code, reason));

    // 状態を条件にした更新で行が返らなかった理由 (ない、または状態が合わない)
    private async ValueTask<PurchaseOrderResult> ResolveFailureAsync(Guid id, Func<PurchaseOrderEntity, RuleError?> validate, CancellationToken cancellationToken)
    {
        var current = await purchaseOrderAccessor.QueryAsync(id, cancellationToken);
        return current is null
            ? new PurchaseOrderResult(PurchaseOrderResultStatus.NotFound)
            : new PurchaseOrderResult(PurchaseOrderResultStatus.Violation, Violation: validate(current) ?? new RuleError(ErrorCode.PurchaseOrderStatusInvalid, RuleReason.PurchaseOrderNotOpen));
    }

    // 明細の商品 (ない商品があれば null)
    private async ValueTask<Dictionary<Guid, ProductEntity>?> QueryProductsAsync(IReadOnlyList<PurchaseOrderLineEntity> lines, CancellationToken cancellationToken)
    {
        var products = (await productAccessor.QueryListByIdsAsync(lines.Select(static x => x.ProductId).Distinct(), cancellationToken)).ToDictionary(static x => x.Id);
        return lines.All(x => products.ContainsKey(x.ProductId)) ? products : null;
    }

    // 明細に商品の写しを付け、番号を振り直す
    private static List<PurchaseOrderLineEntity> ToLines(Guid purchaseOrderId, IEnumerable<PurchaseOrderLineEntity> lines, Dictionary<Guid, ProductEntity> products) =>
        lines.Select((x, i) => new PurchaseOrderLineEntity
        {
            Id = Guid.CreateVersion7(),
            PurchaseOrderId = purchaseOrderId,
            LineNo = i + 1,
            ProductId = x.ProductId,
            ProductCode = products[x.ProductId].Code,
            ProductName = products[x.ProductId].Name,
            Quantity = x.Quantity,
            Cost = x.Cost
        }).ToList();

    // 入荷予定の明細は発注の明細と同じ番号にする (受領した数を発注の明細に引くため)
    private static InventoryReceiptLineEntity ToReceiptLine(Guid receiptId, PurchaseOrderLineEntity line) =>
        new()
        {
            Id = Guid.CreateVersion7(),
            ReceiptId = receiptId,
            LineNo = line.LineNo,
            ProductId = line.ProductId,
            ProductCode = line.ProductCode,
            ProductName = line.ProductName,
            Quantity = line.Quantity,
            Cost = line.Cost
        };

    private async ValueTask<Dictionary<Guid, string>> QuerySupplierNamesAsync(CancellationToken cancellationToken) =>
        (await masterAccessor.QuerySupplierListAsync(true, cancellationToken)).ToDictionary(static x => x.Id, static x => x.Name);

    private async ValueTask<PurchaseOrderDetailView> LoadDetailAsync(PurchaseOrderEntity order, Dictionary<Guid, string> suppliers, CancellationToken cancellationToken) =>
        new()
        {
            PurchaseOrder = order,
            SupplierName = suppliers.GetValueOrDefault(order.SupplierId, String.Empty),
            Lines = await purchaseOrderAccessor.QueryLineListAsync(order.Id, cancellationToken),
            ReceivedQuantities = await QueryReceivedQuantitiesAsync(order.ReceiptId, cancellationToken)
        };

    // 入荷予定で受領した数 (明細番号ごと。受領の前は空)
    private async ValueTask<Dictionary<int, decimal>> QueryReceivedQuantitiesAsync(Guid? receiptId, CancellationToken cancellationToken) =>
        receiptId is null
            ? []
            : (await receiptAccessor.QueryLineListAsync(receiptId.Value, cancellationToken))
                .Where(static x => x.ReceivedQuantity is not null)
                .ToDictionary(static x => x.LineNo, static x => x.ReceivedQuantity!.Value);
}
