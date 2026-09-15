namespace Pos.Server.Services;

using Pos.Server.Accessors;
using Pos.Server.Models;
using Pos.Server.Models.Entity;
using Pos.Server.Models.Parameters;
using Pos.Server.Models.Views;

// 棚卸・調整の結果。同じ id の再送は Duplicate (登録済みの内容を返す)
public sealed record InventoryChangeResult(InventoryChangeEntity Change, bool Duplicate);

// 在庫。取引による変動は TransactionService が書き、ここでは棚卸・調整と照会を扱う
public sealed class InventoryService
{
    private readonly IDbProvider provider;
    private readonly IDialect dialect;
    private readonly ProductAccessor productAccessor;
    private readonly InventoryAccessor inventoryAccessor;
    private readonly TimeProvider timeProvider;

    public InventoryService(
        IDbProvider provider,
        IDialect dialect,
        ProductAccessor productAccessor,
        InventoryAccessor inventoryAccessor,
        TimeProvider timeProvider)
    {
        this.provider = provider;
        this.dialect = dialect;
        this.productAccessor = productAccessor;
        this.inventoryAccessor = inventoryAccessor;
        this.timeProvider = timeProvider;
    }

    //--------------------------------------------------------------------------------
    // Level
    //--------------------------------------------------------------------------------

    // 差分同期 (UpdatedSince 指定時) は更新日時順、通常は商品コード順
    public async ValueTask<PagedResult<InventoryLevelEntity>> QueryLevelPageAsync(InventoryLevelQueryParameter parameter, CancellationToken cancellationToken)
    {
        var total = await inventoryAccessor.CountLevelsAsync(parameter.StoreId, parameter.ProductId, parameter.CategoryId, parameter.NegativeOnly, parameter.UpdatedSince, cancellationToken);
        var items = await inventoryAccessor.QueryLevelListAsync(parameter.StoreId, parameter.ProductId, parameter.CategoryId, parameter.NegativeOnly, parameter.UpdatedSince, parameter.Size, parameter.Page * parameter.Size, cancellationToken);
        return new PagedResult<InventoryLevelEntity>((int)total, parameter.Page, parameter.Size, items);
    }

    // 商品の全店舗在庫 (他店在庫照会)。商品がなければ null
    public async ValueTask<List<ProductInventoryLevelView>?> QueryProductLevelsAsync(Guid productId, CancellationToken cancellationToken)
    {
        if (await productAccessor.QueryAsync(productId, cancellationToken) is null)
        {
            return null;
        }

        return await inventoryAccessor.QueryLevelsByProductAsync(productId, cancellationToken);
    }

    // 現在庫一覧 (店舗名・商品名付き)
    public async ValueTask<PagedResult<InventoryLevelDetailView>> QueryLevelDetailPageAsync(InventoryLevelDetailQueryParameter parameter, CancellationToken cancellationToken)
    {
        var keyword = ServiceHelper.ToLikePattern(dialect, parameter.Keyword);
        var total = await inventoryAccessor.CountLevelDetailsAsync(parameter.StoreId, parameter.CategoryId, keyword, parameter.NegativeOnly, cancellationToken);
        var items = await inventoryAccessor.QueryLevelDetailListAsync(parameter.StoreId, parameter.CategoryId, keyword, parameter.NegativeOnly, parameter.Sort, parameter.Desc, parameter.Size, parameter.Page * parameter.Size, cancellationToken);
        return new PagedResult<InventoryLevelDetailView>((int)total, parameter.Page, parameter.Size, items);
    }

    //--------------------------------------------------------------------------------
    // Change
    //--------------------------------------------------------------------------------

    // 棚卸 (絶対数量) と調整 (増減) の一括登録。同じ id は Duplicate として登録済みの結果を返す
    public async ValueTask<List<InventoryChangeResult>> ApplyChangesAsync(IReadOnlyList<InventoryChangeParameter> changes, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var results = new List<InventoryChangeResult>(changes.Count);
        foreach (var change in changes)
        {
            var existing = await inventoryAccessor.QueryChangeAsync(change.Id, cancellationToken);
            results.Add(existing is not null
                ? new InventoryChangeResult(existing, true)
                : new InventoryChangeResult(await ApplyChangeAsync(change, now, cancellationToken), false));
        }

        return results;
    }

    public ValueTask<InventoryChangeEntity> ApplyChangeAsync(InventoryChangeParameter change, CancellationToken cancellationToken) =>
        ApplyChangeAsync(change, timeProvider.GetUtcNow().UtcDateTime, cancellationToken);

    public async ValueTask<PagedResult<InventoryChangeEntity>> QueryChangePageAsync(InventoryChangeQueryParameter parameter, CancellationToken cancellationToken)
    {
        var total = await inventoryAccessor.CountChangesAsync(parameter.StoreId, parameter.ProductId, parameter.Type, parameter.From, parameter.To, cancellationToken);
        var items = await inventoryAccessor.QueryChangeListAsync(parameter.StoreId, parameter.ProductId, parameter.Type, parameter.From, parameter.To, parameter.Size, parameter.Page * parameter.Size, cancellationToken);
        return new PagedResult<InventoryChangeEntity>((int)total, parameter.Page, parameter.Size, items);
    }

    // 1 トランザクションで在庫を加減算し、変動履歴を残す。PhysicalCount は quantityDelta = quantity − 現在庫
    private ValueTask<InventoryChangeEntity> ApplyChangeAsync(InventoryChangeParameter change, DateTime now, CancellationToken cancellationToken)
    {
        return provider.UsingTxAsync(async (_, tx) =>
        {
            var delta = change.Type == InventoryChangeType.PhysicalCount
                ? change.Quantity - ((await inventoryAccessor.QueryLevelAsync(tx, change.StoreId, change.ProductId, cancellationToken))?.Quantity ?? 0m)
                : change.Quantity;
            var after = await inventoryAccessor.AddQuantityAsync(tx, change.StoreId, change.ProductId, delta, now, cancellationToken);
            var entity = new InventoryChangeEntity
            {
                Id = change.Id,
                StoreId = change.StoreId,
                ProductId = change.ProductId,
                Type = change.Type,
                QuantityDelta = delta,
                QuantityAfter = after,
                ReasonId = change.ReasonId,
                Reason = change.Reason,
                StaffId = change.StaffId,
                OccurredAt = change.OccurredAt,
                CreatedAt = now
            };
            await inventoryAccessor.InsertChangeAsync(tx, entity, cancellationToken);
            await tx.CommitAsync(cancellationToken);
            return entity;
        }, cancellationToken);
    }
}
