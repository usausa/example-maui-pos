namespace Pos.Server.Models.Views;

// シフトのポイント付与・利用
public sealed record PointTotalsView(int Earned, int Redeemed)
{
    public static PointTotalsView Empty { get; } = new(0, 0);
}
