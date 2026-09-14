namespace Pos.Server.Models.Views;

// 売上日報 PDF の入力 (店舗 × 営業日)。Summary は取引がなければ null
public sealed class DailySalesReport
{
    public required string StoreCode { get; init; }

    public required string StoreName { get; init; }

    public required DateOnly BusinessDate { get; init; }

    public required TimeZoneInfo TimeZone { get; init; }

    public SalesSummaryRow? Summary { get; init; }

    public required IReadOnlyList<SalesSummaryRow> ByPaymentMethod { get; init; }

    public required IReadOnlyList<SalesSummaryRow> ByTaxRate { get; init; }

    public required IReadOnlyList<SalesSummaryRow> ByCategory { get; init; }

    public required IReadOnlyList<SalesSummaryRow> ByHour { get; init; }

    public required IReadOnlyList<DailySalesReportShift> Shifts { get; init; }
}

public sealed record DailySalesReportShift(
    string TerminalName,
    string StaffName,
    DateTime OpenedAt,
    DateTime? ClosedAt,
    decimal? ExpectedCash,
    decimal? ActualCash,
    decimal? Difference);
