namespace Pos.Server.Host.Reports;

using OysterReport;

using Pos.Server.Host.Infrastructure.Reports;
using Pos.Server.Models.Views;

// 売上日報 PDF。Assets/Reports/DailySalesReport.xlsx
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

    public byte[] Build(DailySalesReportView report)
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

    private void EditSheet(TemplateSheet sheet, DailySalesReportView report)
    {
        var timeZone = report.TimeZone;
        var summary = report.Summary;
        var transactionCount = summary?.TransactionCount ?? 0;
        var netSales = summary?.NetSales ?? 0m;
        sheet.ReplacePlaceholders(new Dictionary<string, string?>
        {
            ["StoreName"] = report.StoreName,
            ["BusinessDate"] = ReportText.Date(report.BusinessDate),
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

        ReportText.FillRows(sheet, "PmName", report.ByPaymentMethod.Select(static x => new Dictionary<string, string?>
        {
            ["PmName"] = x.GroupLabel,
            ["PmSales"] = ReportText.Yen(x.SalesTotal),
            ["PmReturns"] = ReportText.Yen(x.ReturnsTotal),
            ["PmNet"] = ReportText.Yen(x.NetSales)
        }).ToList());

        ReportText.FillRows(sheet, "TaxLabel", report.ByTaxRate.Select(static x => new Dictionary<string, string?>
        {
            ["TaxLabel"] = x.GroupLabel,
            ["TaxableAmount"] = ReportText.Yen(x.TaxableAmount ?? 0m),
            ["TaxAmount"] = ReportText.Yen(x.TaxAmount ?? 0m)
        }).ToList());

        ReportText.FillRows(sheet, "CatName", report.ByCategory.Select(static x => new Dictionary<string, string?>
        {
            ["CatName"] = x.GroupLabel,
            ["CatCount"] = ReportText.Count(x.TransactionCount),
            ["CatNetSales"] = ReportText.Yen(x.NetSales)
        }).ToList());

        ReportText.FillRows(sheet, "HourLabel", report.ByHour.Select(static x => new Dictionary<string, string?>
        {
            ["HourLabel"] = x.GroupLabel,
            ["HourCount"] = ReportText.Count(x.TransactionCount),
            ["HourNetSales"] = ReportText.Yen(x.NetSales)
        }).ToList());

        ReportText.FillRows(sheet, "ShTerminal", report.Shifts.Select(x => new Dictionary<string, string?>
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
