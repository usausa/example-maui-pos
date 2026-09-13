namespace Pos.Server.Host.Infrastructure.Reports;

using OysterReport;

using Pos.Server.Models;

// 売上日報の入力 (店舗 × 営業日)。Summary は取引がなければ null
public sealed record DailySalesReportData(
    string StoreName,
    DateOnly BusinessDate,
    TimeZoneInfo TimeZone,
    SalesSummaryRow? Summary,
    IReadOnlyList<SalesSummaryRow> ByPaymentMethod,
    IReadOnlyList<SalesSummaryRow> ByTaxRate,
    IReadOnlyList<SalesSummaryRow> ByCategory,
    IReadOnlyList<SalesSummaryRow> ByHour,
    IReadOnlyList<DailySalesReportShift> Shifts);

public sealed record DailySalesReportShift(
    string TerminalName,
    string StaffName,
    DateTime OpenedAt,
    DateTime? ClosedAt,
    decimal? ExpectedCash,
    decimal? ActualCash,
    decimal? Difference);

// 売上日報 PDF (D-37)。Assets/Reports/DailySalesReport.xlsx
public sealed class DailySalesReportBuilder
{
    private const string TemplatePath = "Assets/Reports/DailySalesReport.xlsx";

    private const string FontPath = "Assets/Fonts/ipaexg.ttf";

    private readonly EmbeddedFontResolver fontResolver = new(FontPath);

    private readonly TimeProvider timeProvider;

    public DailySalesReportBuilder(TimeProvider timeProvider)
    {
        this.timeProvider = timeProvider;
    }

    public byte[] Build(DailySalesReportData data)
    {
        ArgumentNullException.ThrowIfNull(data);

        var engine = new OysterReportEngine
        {
            FontResolver = fontResolver
        };

        using var workbook = new TemplateWorkbook(TemplatePath);
        EditSheet(workbook.Sheets[0], data);

        using var output = new MemoryStream();
        engine.GeneratePdf(workbook, output);
        return output.ToArray();
    }

    private void EditSheet(TemplateSheet sheet, DailySalesReportData data)
    {
        var timeZone = data.TimeZone;
        var summary = data.Summary;
        var transactionCount = summary?.TransactionCount ?? 0;
        var netSales = summary?.NetSales ?? 0m;

        sheet.ReplacePlaceholders(new Dictionary<string, string?>
        {
            ["StoreName"] = data.StoreName,
            ["BusinessDate"] = ReportText.Date(data.BusinessDate),
            ["IssuedAt"] = ReportText.DateTime(timeProvider.GetUtcNow().UtcDateTime, timeZone),
            ["SalesTotal"] = ReportText.Yen(summary?.SalesTotal ?? 0m),
            ["ReturnsTotal"] = ReportText.Yen(summary?.ReturnsTotal ?? 0m),
            ["NetSales"] = ReportText.Yen(netSales),
            ["DiscountTotal"] = ReportText.Yen(summary?.DiscountTotal ?? 0m),
            ["TaxTotal"] = ReportText.Yen(summary?.TaxTotal ?? 0m),
            ["TransactionCount"] = ReportText.Count(transactionCount),
            ["ReturnCount"] = ReportText.Count(summary?.ReturnCount ?? 0),
            ["AveragePerCustomer"] = transactionCount == 0 ? "-" : ReportText.Yen(Math.Floor(netSales / transactionCount)),
            ["PointsEarned"] = ReportText.Count(summary?.PointsEarned ?? 0),
            ["PointsRedeemed"] = ReportText.Count(summary?.PointsRedeemed ?? 0)
        });

        ReportText.FillRows(sheet, "PmName", data.ByPaymentMethod.Select(static x => new Dictionary<string, string?>
        {
            ["PmName"] = x.GroupLabel,
            ["PmSales"] = ReportText.Yen(x.SalesTotal),
            ["PmReturns"] = ReportText.Yen(x.ReturnsTotal),
            ["PmNet"] = ReportText.Yen(x.NetSales)
        }).ToList());

        ReportText.FillRows(sheet, "TaxLabel", data.ByTaxRate.Select(static x => new Dictionary<string, string?>
        {
            ["TaxLabel"] = x.GroupLabel,
            ["TaxableAmount"] = ReportText.Yen(x.TaxableAmount ?? 0m),
            ["TaxAmount"] = ReportText.Yen(x.TaxAmount ?? 0m)
        }).ToList());

        ReportText.FillRows(sheet, "CatName", data.ByCategory.Select(static x => new Dictionary<string, string?>
        {
            ["CatName"] = x.GroupLabel,
            ["CatCount"] = ReportText.Count(x.TransactionCount),
            ["CatNetSales"] = ReportText.Yen(x.NetSales)
        }).ToList());

        ReportText.FillRows(sheet, "HourLabel", data.ByHour.Select(static x => new Dictionary<string, string?>
        {
            ["HourLabel"] = x.GroupLabel,
            ["HourCount"] = ReportText.Count(x.TransactionCount),
            ["HourNetSales"] = ReportText.Yen(x.NetSales)
        }).ToList());

        ReportText.FillRows(sheet, "ShTerminal", data.Shifts.Select(x => new Dictionary<string, string?>
        {
            ["ShTerminal"] = x.TerminalName,
            ["ShStaff"] = x.StaffName,
            ["ShOpenedAt"] = ReportText.Time(x.OpenedAt, timeZone),
            ["ShClosedAt"] = ReportText.Time(x.ClosedAt, timeZone),
            ["ShExpected"] = ReportText.Yen(x.ExpectedCash),
            ["ShActual"] = ReportText.Yen(x.ActualCash),
            ["ShDifference"] = ReportText.Yen(x.Difference)
        }).ToList());
    }
}
