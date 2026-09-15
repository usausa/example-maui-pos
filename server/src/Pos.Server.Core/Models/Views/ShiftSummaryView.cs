namespace Pos.Server.Models.Views;

// 精算レポートの内容 (シフトの集計 + 支払方法別・税率別・部門別・ポイント)
public sealed class ShiftSummaryView
{
    public required ShiftDetailView Shift { get; init; }

    public required IReadOnlyList<PaymentMethodTotalView> ByPaymentMethod { get; init; }

    public required IReadOnlyList<TaxRateTotalView> ByTaxRate { get; init; }

    public required IReadOnlyList<CategoryTotalView> ByCategory { get; init; }

    public required PointTotalsView Points { get; init; }
}
