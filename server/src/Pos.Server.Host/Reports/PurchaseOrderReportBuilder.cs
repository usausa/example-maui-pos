namespace Pos.Server.Host.Reports;

using OysterReport;

using Pos.Server.Host.Infrastructure.Reports;
using Pos.Server.Models.Views;

// 発注書 PDF。Assets/Reports/PurchaseOrder.xlsx。仕入先へは人が送る (EDI やメールでの送信は作らない)
public sealed class PurchaseOrderReportBuilder
{
    private const string TemplatePath = "Assets/Reports/PurchaseOrder.xlsx";
    private const string FontPath = "Assets/Fonts/ipaexg.ttf";

    private readonly EmbeddedFontResolver fontResolver = new(FontPath);

    private readonly TimeProvider timeProvider;

    public PurchaseOrderReportBuilder(TimeProvider timeProvider)
    {
        this.timeProvider = timeProvider;
    }

    public byte[] Build(PurchaseOrderReportView report)
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

    private void EditSheet(TemplateSheet sheet, PurchaseOrderReportView report)
    {
        var detail = report.Detail;
        var order = detail.PurchaseOrder;
        var store = report.Store;

        // 発注元の住所・電話と備考は、ないときは行ごと消す
        FillOptional(sheet, "StoreAddress", String.IsNullOrEmpty(store?.Address) ? null : $"{store.PostalCode} {store.Address}".Trim());
        FillOptional(sheet, "StorePhone", String.IsNullOrEmpty(store?.Phone) ? null : "TEL " + store.Phone);
        FillOptional(sheet, "Note", order.Note);
        sheet.ReplacePlaceholders(new Dictionary<string, string?>
        {
            ["Title"] = Title(order.Status),
            ["PurchaseOrderNo"] = "発注番号 " + order.PurchaseOrderNo,
            ["OrderedAt"] = "発注日 " + (order.OrderedAt is null ? "(未発注)" : ReportText.DateTime(order.OrderedAt, report.TimeZone)),
            ["SupplierName"] = detail.SupplierName + " 御中",
            ["CompanyName"] = report.CompanyName,
            ["StoreName"] = store?.Name ?? String.Empty,
            ["ExpectedDate"] = order.ExpectedDate is null ? "指定なし" : ReportText.Date(order.ExpectedDate.Value),
            ["DeliverTo"] = store?.Name ?? String.Empty,
            ["TotalCost"] = "¥" + ReportText.Yen(detail.TotalCost),
            ["IssuedAt"] = "発行 " + ReportText.DateTime(timeProvider.GetUtcNow().UtcDateTime, report.TimeZone)
        });

        ReportText.FillRows(sheet, "LineNo", detail.Lines.Select(static x => new Dictionary<string, string?>
        {
            ["LineNo"] = x.LineNo.ToString(CultureInfo.InvariantCulture),
            ["ProductCode"] = x.ProductCode,
            ["ProductName"] = x.ProductName,
            ["Quantity"] = ReportText.Quantity(x.Quantity),
            ["Cost"] = ReportText.Yen(x.Cost),
            ["Amount"] = x.Cost is null ? String.Empty : ReportText.Yen(x.Quantity * x.Cost.Value)
        }).ToList());
    }

    // 下書きとキャンセルは表題に状態を付ける (送る前の確認や控えと区別する)
    private static string Title(PurchaseOrderStatus status) =>
        status switch
        {
            PurchaseOrderStatus.Draft => "発注書 (下書き)",
            PurchaseOrderStatus.Cancelled => "発注書 (キャンセル)",
            _ => "発注書"
        };

    private static void FillOptional(TemplateSheet sheet, string key, string? value) =>
        ReportText.FillRows(sheet, key, String.IsNullOrEmpty(value) ? [] : [new Dictionary<string, string?> { [key] = value }]);
}
