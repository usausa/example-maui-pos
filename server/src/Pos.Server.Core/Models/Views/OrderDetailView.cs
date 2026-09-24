namespace Pos.Server.Models.Views;

using Pos.Server.Models.Entity;

// 受注と明細
public sealed class OrderDetailView
{
    public required OrderEntity Order { get; init; }

    public required IReadOnlyList<OrderLineEntity> Lines { get; init; }
}
