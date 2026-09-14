namespace Pos.Server.Models.Views;

// 精算レポートの内容 (シフトの集計 + 支払方法別・税率別・部門別・ポイント)
public sealed class ShiftSummary
{
    public required ShiftDetail Shift { get; init; }

    public required IReadOnlyList<PaymentMethodTotal> ByPaymentMethod { get; init; }

    public required IReadOnlyList<TaxRateTotal> ByTaxRate { get; init; }

    public required IReadOnlyList<CategoryTotal> ByCategory { get; init; }

    public required PointTotals Points { get; init; }
}
