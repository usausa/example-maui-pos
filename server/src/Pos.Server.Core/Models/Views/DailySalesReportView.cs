namespace Pos.Server.Models.Views;

// 売上日報 PDF の入力 (店舗 × 営業日)。Summary は取引がなければ null
public sealed class DailySalesReportView
{
    public required string StoreCode { get; init; }

    public required string StoreName { get; init; }

    public required DateOnly BusinessDate { get; init; }

    public required TimeZoneInfo TimeZone { get; init; }

    public SalesSummaryView? Summary { get; init; }

    public required IReadOnlyList<SalesSummaryView> ByPaymentMethod { get; init; }

    public required IReadOnlyList<SalesSummaryView> ByTaxRate { get; init; }

    public required IReadOnlyList<SalesSummaryView> ByCategory { get; init; }

    public required IReadOnlyList<SalesSummaryView> ByHour { get; init; }

    public required IReadOnlyList<DailySalesReportViewShift> Shifts { get; init; }
}

public sealed record DailySalesReportViewShift(
    string TerminalName,
    string StaffName,
    DateTime OpenedAt,
    DateTime? ClosedAt,
    decimal? ExpectedCash,
    decimal? ActualCash,
    decimal? Difference);
