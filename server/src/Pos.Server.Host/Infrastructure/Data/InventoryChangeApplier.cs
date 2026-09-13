namespace Pos.Server.Host.Infrastructure.Data;

using Pos.Server.Accessors;
using Pos.Server.Models.Entity;

using Smart.Data;

// 棚卸 (絶対数量) / 調整 (増減) の適用。API (POST /inventory/changes) と管理画面 (S-43) で同じ処理
public static class InventoryChangeApplier
{
    // 1 トランザクションで在庫を加減算し、変動履歴を残す。PhysicalCount は quantityDelta = quantity − 現在庫
    public static ValueTask<InventoryChangeEntity> ApplyAsync(
        InventoryAccessor accessor,
        IDbProvider provider,
        InventoryChangeEntity change,
        decimal quantity,
        DateTime now,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(accessor);
        ArgumentNullException.ThrowIfNull(provider);
        ArgumentNullException.ThrowIfNull(change);

        return provider.UsingTxAsync(async (_, tx) =>
        {
            var delta = change.Type == InventoryChangeType.PhysicalCount
                ? quantity - ((await accessor.QueryLevelAsync(tx, change.StoreId, change.ProductId, cancellationToken))?.Quantity ?? 0m)
                : quantity;
            var after = await accessor.AddQuantityAsync(tx, change.StoreId, change.ProductId, delta, now, cancellationToken);
            change.QuantityDelta = delta;
            change.QuantityAfter = after;
            change.CreatedAt = now;
            await accessor.InsertChangeAsync(tx, change, cancellationToken);
            await tx.CommitAsync(cancellationToken);
            return change;
        }, cancellationToken);
    }
}
