namespace Pos.Terminal.Usecases;

using Pos.Contract.Inventory;
using Pos.Terminal.Models.Entity;

using Smart.Data;

// 棚卸・在庫調整: 商品の検索、現在庫、在庫変動の送信 (ローカルの在庫キャッシュも同じように動かす)
public sealed class StockUsecase
{
    private readonly IDbProvider provider;

    private readonly DataAccessor accessor;

    private readonly Session session;

    private readonly SyncService sync;

    public StockUsecase(
        IDbProvider provider,
        DataAccessor accessor,
        Session session,
        SyncService sync)
    {
        this.provider = provider;
        this.accessor = accessor;
        this.session = session;
        this.sync = sync;
    }

    public async ValueTask<ProductResponseItem?> FindProductAsync(string code) =>
        await accessor.QueryProductByBarcodeAsync(code) ?? await accessor.QueryProductByCodeAsync(code);

    public async ValueTask<decimal> QueryQuantityAsync(Guid productId)
    {
        if (session.Store is null)
        {
            return 0m;
        }

        var level = await accessor.QueryInventoryLevelAsync(session.Store.Id, productId);
        return level?.Quantity ?? 0m;
    }

    public ValueTask<List<AdjustmentReasonResponseItem>> QueryReasonListAsync() => accessor.QueryAdjustmentReasonListAsync();

    // 店舗・担当が決まっているときだけ呼ぶ
    public async ValueTask SendAsync(IReadOnlyList<StockChange> changes)
    {
        var now = DateTime.UtcNow;
        var storeId = session.Store!.Id;
        var staffId = session.Staff!.Id;
        var request = new InventoryChangeRequest
        {
            Changes = changes.Select(x => new InventoryChangeRequestChange
            {
                Id = x.Id,
                StoreId = storeId,
                ProductId = x.Product.Id,
                Type = x.Type,
                Quantity = x.Quantity,
                ReasonId = x.ReasonId,
                Reason = x.Reason,
                StaffId = staffId,
                OccurredAt = now
            }).ToList()
        };
        await provider.UsingTxAsync(async (_, tx) =>
        {
            foreach (var change in changes)
            {
                if (change.Type == InventoryChangeType.PhysicalCount)
                {
                    await accessor.UpdateInventoryQuantityAsync(tx, storeId, change.Product.Id, change.Quantity, now);
                }
                else
                {
                    await accessor.AddInventoryQuantityAsync(tx, storeId, change.Product.Id, change.Quantity, now);
                }
            }

            await accessor.InsertOutboxAsync(tx, SyncService.CreateEntry(OutboxKind.InventoryChanges, Guid.CreateVersion7(), request, now));
            await tx.CommitAsync();
        });
        await sync.UpdateCountsAsync();
        sync.Trigger();
    }
}
