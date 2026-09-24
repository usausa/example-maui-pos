namespace Pos.Server.Models.Views;

using Pos.Server.Models.Entity;

// 店舗間移動と明細 (出荷店・入荷店の名前付き)
public sealed class InventoryTransferDetailView
{
    public required InventoryTransferEntity Transfer { get; init; }

    public required string FromStoreName { get; init; }

    public required string ToStoreName { get; init; }

    public required IReadOnlyList<InventoryTransferLineEntity> Lines { get; init; }
}
