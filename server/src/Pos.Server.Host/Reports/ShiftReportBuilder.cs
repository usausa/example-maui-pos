namespace Pos.Server.Host.Reports;

using OysterReport;

using Pos.Server.Host.Infrastructure.Reports;
using Pos.Server.Models.Views;

// 精算レポート PDF。Assets/Reports/ShiftReport.xlsx の 1 シート = 1 シフト
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

    public byte[] Build(ShiftReportView report)
    {
        var engine = new OysterReportEngine
        {
            FontResolver = fontResolver
        };
        using var workbook = new TemplateWorkbook(TemplatePath);
        EditSheet(workbook.Sheets[0], report);

        using var output = new MemoryStream();
        engine.GeneratePdf(workbook, output);
        return output.ToArray();
    }

    private void EditSheet(TemplateSheet sheet, ShiftReportView report)
    {
        var summary = report.Summary;
        var detail = summary.Shift;
        var shift = detail.Shift;
        var totals = detail.Totals;
        var timeZone = report.TimeZone;
        sheet.ReplacePlaceholders(new Dictionary<string, string?>
        {
            ["StoreName"] = report.StoreName,
            ["TerminalName"] = report.TerminalName,
            ["BusinessDate"] = ReportText.Date(shift.BusinessDate),
            ["Status"] = shift.Status == ShiftStatus.Closed ? "精算済み" : "開設中",
            ["OpenedAt"] = ReportText.DateTime(shift.OpenedAt, timeZone),
            ["OpenedBy"] = report.OpenedBy,
            ["ClosedAt"] = ReportText.DateTime(shift.ClosedAt, timeZone),
            ["ClosedBy"] = report.ClosedBy ?? String.Empty,
            ["IssuedAt"] = ReportText.DateTime(timeProvider.GetUtcNow().UtcDateTime, timeZone),
            ["OpeningCash"] = ReportText.Yen(shift.OpeningCash),
            ["CashSales"] = ReportText.Yen(totals.CashSales),
            ["CashReturns"] = ReportText.Yen(totals.CashReturns),
            ["PaidIn"] = ReportText.Yen(totals.PaidIn),
            ["PaidOut"] = ReportText.Yen(totals.PaidOut),
            ["DepositCashIn"] = ReportText.Yen(totals.DepositCashIn),
            ["DepositCashOut"] = ReportText.Yen(totals.DepositCashOut),
            ["ExpectedCash"] = ReportText.Yen(detail.ExpectedCash),
            ["ActualCash"] = ReportText.Yen(shift.ActualCash),
            ["Difference"] = ReportText.Yen(shift.Difference),
            ["SalesCount"] = ReportText.Count(totals.SalesCount),
            ["ReturnCount"] = ReportText.Count(totals.ReturnCount),
            ["VoidCount"] = ReportText.Count(totals.VoidCount),
            ["SalesTotal"] = ReportText.Yen(totals.SalesTotal),
            ["ReturnsTotal"] = ReportText.Yen(totals.ReturnsTotal),
            ["NetSales"] = ReportText.Yen(totals.SalesTotal - totals.ReturnsTotal),
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

        ReportText.FillRows(sheet, "DenName", detail.Denominations.Select(static x => new Dictionary<string, string?>
        {
            ["DenName"] = ReportText.Yen(x.Denomination) + " 円",
            ["DenCount"] = ReportText.Count(x.Count),
            ["DenAmount"] = ReportText.Yen(x.Denomination * (decimal)x.Count)
        }).ToList());
    }
}
