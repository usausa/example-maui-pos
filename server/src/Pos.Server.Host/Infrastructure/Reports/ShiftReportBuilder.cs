namespace Pos.Server.Host.Infrastructure.Reports;

using OysterReport;

using Pos.Shared.Shifts;

// 精算レポートの入力 (シフトの集計 + 表示名)
public sealed record ShiftReportData(
    ShiftSummaryResponse Summary,
    string StoreName,
    string TerminalName,
    string OpenedBy,
    string? ClosedBy,
    TimeZoneInfo TimeZone);

// 精算レポート PDF (D-37)。Assets/Reports/ShiftReport.xlsx の 1 シート = 1 シフト
public sealed class ShiftReportBuilder
{
    private const string TemplatePath = "Assets/Reports/ShiftReport.xlsx";

    private const string FontPath = "Assets/Fonts/ipaexg.ttf";

    private readonly EmbeddedFontResolver fontResolver = new(FontPath);

    private readonly TimeProvider timeProvider;

    public ShiftReportBuilder(TimeProvider timeProvider)
    {
        this.timeProvider = timeProvider;
    }

    public byte[] Build(ShiftReportData data)
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

    private void EditSheet(TemplateSheet sheet, ShiftReportData data)
    {
        var summary = data.Summary;
        var shift = summary.Shift;
        var timeZone = data.TimeZone;

        sheet.ReplacePlaceholders(new Dictionary<string, string?>
        {
            ["StoreName"] = data.StoreName,
            ["TerminalName"] = data.TerminalName,
            ["BusinessDate"] = ReportText.Date(shift.BusinessDate),
            ["Status"] = shift.Status == ShiftStatus.Closed ? "精算済み" : "開設中",
            ["OpenedAt"] = ReportText.DateTime(shift.OpenedAt, timeZone),
            ["OpenedBy"] = data.OpenedBy,
            ["ClosedAt"] = ReportText.DateTime(shift.ClosedAt, timeZone),
            ["ClosedBy"] = data.ClosedBy ?? String.Empty,
            ["IssuedAt"] = ReportText.DateTime(timeProvider.GetUtcNow().UtcDateTime, timeZone),
            ["OpeningCash"] = ReportText.Yen(summary.Cash.OpeningCash),
            ["CashSales"] = ReportText.Yen(summary.Cash.CashSales),
            ["CashReturns"] = ReportText.Yen(summary.Cash.CashReturns),
            ["PaidIn"] = ReportText.Yen(summary.Cash.PaidIn),
            ["PaidOut"] = ReportText.Yen(summary.Cash.PaidOut),
            ["ExpectedCash"] = ReportText.Yen(summary.Cash.ExpectedCash),
            ["ActualCash"] = ReportText.Yen(summary.Cash.ActualCash),
            ["Difference"] = ReportText.Yen(summary.Cash.Difference),
            ["SalesCount"] = ReportText.Count(shift.Totals.SalesCount),
            ["ReturnCount"] = ReportText.Count(shift.Totals.ReturnCount),
            ["VoidCount"] = ReportText.Count(shift.Totals.VoidCount),
            ["SalesTotal"] = ReportText.Yen(shift.Totals.SalesTotal),
            ["ReturnsTotal"] = ReportText.Yen(shift.Totals.ReturnsTotal),
            ["NetSales"] = ReportText.Yen(shift.Totals.SalesTotal - shift.Totals.ReturnsTotal),
            ["PointsEarned"] = ReportText.Count(summary.Points.Earned),
            ["PointsRedeemed"] = ReportText.Count(summary.Points.Redeemed),
            ["Note"] = shift.Note ?? String.Empty
        });

        ReportText.FillRows(sheet, "PmName", summary.ByPaymentMethod.Select(static x => new Dictionary<string, string?>
        {
            ["PmName"] = x.Name,
            ["PmSalesCount"] = ReportText.Count(x.SalesCount),
            ["PmSalesAmount"] = ReportText.Yen(x.SalesAmount),
            ["PmReturnCount"] = ReportText.Count(x.ReturnCount),
            ["PmReturnAmount"] = ReportText.Yen(x.ReturnAmount)
        }).ToList());

        ReportText.FillRows(sheet, "TaxRate", summary.ByTaxRate.Select(static x => new Dictionary<string, string?>
        {
            ["TaxRate"] = ReportText.Percent(x.Rate),
            ["TaxKind"] = x.TaxIncluded ? "内税" : "外税",
            ["TaxableAmount"] = ReportText.Yen(x.TaxableAmount),
            ["TaxAmount"] = ReportText.Yen(x.TaxAmount)
        }).ToList());

        ReportText.FillRows(sheet, "CatName", summary.ByCategory.Select(static x => new Dictionary<string, string?>
        {
            ["CatName"] = x.Name,
            ["CatQuantity"] = ReportText.Quantity(x.Quantity),
            ["CatNetAmount"] = ReportText.Yen(x.NetAmount)
        }).ToList());

        ReportText.FillRows(sheet, "DenName", shift.Denominations.Select(static x => new Dictionary<string, string?>
        {
            ["DenName"] = ReportText.Yen(x.Denomination) + " 円",
            ["DenCount"] = ReportText.Count(x.Count),
            ["DenAmount"] = ReportText.Yen(x.Denomination * (decimal)x.Count)
        }).ToList());
    }
}
