namespace Pos.Server.Models.Views;

using Pos.Server.Models.Entity;

// 日次締めの内容。内訳は締め済みなら締めた時点、未締めなら取引からの集計。シフトは現在の状態
public sealed class DailyClosingSummaryView
{
    public required DailyClosingDayView Day { get; init; }

    public required IReadOnlyList<PaymentMethodTotalView> ByPaymentMethod { get; init; }

    public required IReadOnlyList<TaxRateTotalView> ByTaxRate { get; init; }

    public required IReadOnlyList<ShiftEntity> Shifts { get; init; }
}
