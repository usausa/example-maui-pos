namespace Pos.Server.Models.Views;

// シフトのポイント付与・利用
public sealed record PointTotals(int Earned, int Redeemed)
{
    public static PointTotals Empty { get; } = new(0, 0);
}
