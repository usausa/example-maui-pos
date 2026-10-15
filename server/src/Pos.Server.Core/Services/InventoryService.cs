namespace Pos.Server.Services;

using Pos.Domain.Logic;
using Pos.Server.Accessors;
using Pos.Server.Models;
using Pos.Server.Models.Entity;
using Pos.Server.Models.Parameters;
using Pos.Server.Models.Views;

// 棚卸・調整の結果。同じ id の再送は Duplicate (登録済みの内容を返す)
public sealed record InventoryChangeResult(InventoryChangeEntity Change, bool Duplicate);

// 一括登録の結果。参照先が見つからない変動があれば、どれも書かずに Violation
public sealed record InventoryChangesResult(IReadOnlyList<InventoryChangeResult> Results, RuleError? Violation = null);

// 在庫。取引による変動は TransactionService が書き、ここでは棚卸・調整と照会を扱う
public sealed class InventoryService
{
    private readonly TimeProvider timeProvider;
    private readonly IDbProvider provider;
    private readonly IDialect dialect;
    private readonly MasterAccessor masterAccessor;
    private readonly ProductAccessor productAccessor;
    private readonly InventoryAccessor inventoryAccessor;
    private readonly ChangeNotificationService changeNotification;

    public InventoryService(
        TimeProvider timeProvider,
        IDbProvider provider,
        IDialect dialect,
        MasterAccessor masterAccessor,
        ProductAccessor productAccessor,
        InventoryAccessor inventoryAccessor,
        ChangeNotificationService changeNotification)
    {
        this.timeProvider = timeProvider;
        this.provider = provider;
        this.dialect = dialect;
        this.masterAccessor = masterAccessor;
        this.productAccessor = productAccessor;
        this.inventoryAccessor = inventoryAccessor;
        this.changeNotification = changeNotification;
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

        return await inventoryAccessor.QueryLevelListByProductAsync(productId, cancellationToken);
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
    public async ValueTask<InventoryChangesResult> ApplyChangesAsync(IReadOnlyList<InventoryChangeParameter> changes, CancellationToken cancellationToken)
    {
        if (await ValidateReferencesAsync(changes, cancellationToken) is { } violation)
        {
            return new InventoryChangesResult([], violation);
        }

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var results = new List<InventoryChangeResult>(changes.Count);
        foreach (var change in changes)
        {
            var existing = await inventoryAccessor.QueryChangeAsync(change.Id, cancellationToken);
            if (existing is not null)
            {
                results.Add(new InventoryChangeResult(existing, true));
                continue;
            }

            try
            {
                results.Add(new InventoryChangeResult(await ApplyChangeAsync(change, now, cancellationToken), false));
            }
            catch (DbException ex) when (dialect.IsDuplicate(ex))
            {
                // 同じ id の再送が同時に届き、先に登録された
                var resent = await inventoryAccessor.QueryChangeAsync(change.Id, cancellationToken);
                if (resent is null)
                {
                    throw;
                }

                results.Add(new InventoryChangeResult(resent, true));
            }
        }

        return new InventoryChangesResult(results);
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
    private async ValueTask<InventoryChangeEntity> ApplyChangeAsync(InventoryChangeParameter change, DateTime now, CancellationToken cancellationToken)
    {
        var applied = await provider.UsingTxAsync(async (_, tx) =>
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
                OccurredAt = change.OccurredAt ?? now,
                CreatedAt = now
            };
            await inventoryAccessor.InsertChangeAsync(tx, entity, cancellationToken);
            await tx.CommitAsync(cancellationToken);
            return entity;
        }, cancellationToken);
        changeNotification.Notify(DataChangeKind.Inventory);
        return applied;
    }

    // 参照先は書く前に確かめる (1 回の棚卸は同じ店舗・担当が並ぶので、異なる値ごとに読む)。端末の記録なので、削除済みも受け付ける
    private async ValueTask<RuleError?> ValidateReferencesAsync(IReadOnlyList<InventoryChangeParameter> changes, CancellationToken cancellationToken)
    {
        foreach (var storeId in changes.Select(static x => x.StoreId).Distinct())
        {
            if (await masterAccessor.QueryStoreAsync(storeId, cancellationToken) is null)
            {
                return new RuleError(ErrorCode.ValidationError, RuleReason.StoreNotFound);
            }
        }

        var productIds = changes.Select(static x => x.ProductId).Distinct().ToList();
        if ((await productAccessor.QueryListByIdsAsync(productIds, cancellationToken)).Count != productIds.Count)
        {
            return new RuleError(ErrorCode.ProductNotFound, RuleReason.ProductNotFound);
        }

        foreach (var reasonId in changes.Select(static x => x.ReasonId).OfType<Guid>().Distinct())
        {
            if (await masterAccessor.QueryAdjustmentReasonAsync(reasonId, cancellationToken) is null)
            {
                return new RuleError(ErrorCode.ValidationError, RuleReason.AdjustmentReasonNotFound);
            }
        }

        foreach (var staffId in changes.Select(static x => x.StaffId).OfType<Guid>().Distinct())
        {
            if (await masterAccessor.QueryStaffAsync(staffId, cancellationToken) is null)
            {
                return new RuleError(ErrorCode.StaffInvalid, RuleReason.StaffNotFound);
            }
        }

        return null;
    }
}
