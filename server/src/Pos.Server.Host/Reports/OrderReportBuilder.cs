namespace Pos.Server.Host.Reports;

using OysterReport;

using Pos.Server.Host.Infrastructure.Reports;
using Pos.Server.Models.Views;

// 受注票 PDF。Assets/Reports/Order.xlsx。お客様に渡す控えで、前受金を受け取ったときは預り証を兼ねる
public sealed class OrderReportBuilder
{
    private const string TemplatePath = "Assets/Reports/Order.xlsx";
    private const string FontPath = "Assets/Fonts/ipaexg.ttf";

    private readonly EmbeddedFontResolver fontResolver = new(FontPath);

    private readonly TimeProvider timeProvider;

    public OrderReportBuilder(TimeProvider timeProvider)
    {
        this.timeProvider = timeProvider;
    }

    public byte[] Build(OrderReportView report)
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

    private void EditSheet(TemplateSheet sheet, OrderReportView report)
    {
        var detail = report.Detail;
        var order = detail.Order;
        var store = report.Store;
        var timeZone = report.TimeZone;
        var open = order.Status.IsOpen();

        // 電話・住所・備考・案内は、ないときは行ごと消す
        FillOptional(sheet, "CustomerPhone", String.IsNullOrEmpty(order.Phone) ? null : "TEL " + order.Phone);
        FillOptional(sheet, "StoreAddress", String.IsNullOrEmpty(store?.Address) ? null : $"{store.PostalCode} {store.Address}".Trim());
        FillOptional(sheet, "StorePhone", String.IsNullOrEmpty(store?.Phone) ? null : "TEL " + store.Phone);
        FillOptional(sheet, "Note", order.Note);
        FillOptional(sheet, "Notice", open ? "お受け取りの際は、この控えをお持ちください。" : null);
        FillOptional(sheet, "DepositNotice", open && (detail.DepositBalance > 0) ? "前受金は、お受け取りのお会計で差し引きます。" : null);
        sheet.ReplacePlaceholders(new Dictionary<string, string?>
        {
            ["Title"] = Title(order.Status),
            ["OrderNo"] = "受注番号 " + order.OrderNo,
            ["OrderedAt"] = "受注日 " + ReportText.DateTime(order.OrderedAt, timeZone),
            ["CustomerName"] = order.CustomerName + " 様",
            ["CompanyName"] = report.CompanyName,
            ["StoreName"] = store?.Name ?? String.Empty,
            ["Greeting"] = order.Type == OrderType.Hold ? "下記の商品をお取り置きしております。" : "下記の商品のお取り寄せを承りました。",
            ["OrderType"] = order.Type.ToDisplayName(),
            ["OrderStatus"] = order.Status.ToDisplayName(),
            ["RequestedDate"] = order.RequestedDate is null ? "指定なし" : ReportText.Date(order.RequestedDate.Value),
            ["StaffName"] = report.StaffName,
            ["TotalText"] = "¥" + ReportText.Yen(order.Total),
            ["Total"] = ReportText.Yen(order.Total),
            ["IssuedAt"] = "発行 " + ReportText.DateTime(timeProvider.GetUtcNow().UtcDateTime, timeZone)
        });

        ReportText.FillRows(sheet, "LineNo", detail.Lines.Select(static x => new Dictionary<string, string?>
        {
            ["LineNo"] = x.LineNo.ToString(CultureInfo.InvariantCulture),
            ["ProductCode"] = x.ProductCode,
            ["ProductName"] = x.ProductName,
            ["Quantity"] = ReportText.Quantity(x.Quantity),
            ["UnitPrice"] = ReportText.Yen(x.UnitPrice),
            ["Amount"] = ReportText.Yen(x.Amount)
        }).ToList());

        // 前受金 (受取と返金の記録、お会計で差し引く額)。ないときは見出しから合計まで消す
        var hasDeposits = detail.Deposits.Count > 0;
        FillOptional(sheet, "DepositTitle", hasDeposits ? "前受金" : null);
        FillOptional(sheet, "DepositHead", hasDeposits ? "日時" : null);
        ReportText.FillRows(sheet, "DepositAt", detail.Deposits.Select(x => new Dictionary<string, string?>
        {
            ["DepositAt"] = ReportText.DateTime(x.OccurredAt, timeZone),
            ["DepositMethod"] = DepositMethod(x.Type, report.PaymentMethodNames.GetValueOrDefault(x.PaymentMethodId) ?? x.Kind.ToDisplayName(), x.Reference),
            ["DepositAmount"] = x.Type == OrderDepositType.Refund ? "-" + ReportText.Yen(x.Amount) : ReportText.Yen(x.Amount)
        }).ToList());
        ReportText.FillRows(sheet, "DepositLabel", hasDeposits ?
        [
            new Dictionary<string, string?>
            {
                ["DepositLabel"] = DepositLabel(order.Status),
                ["DepositTotal"] = ReportText.Yen(open ? detail.DepositBalance : detail.DepositNet)
            }
        ] : []);
    }

    // 完了とキャンセルは表題に状態を付ける (お渡し前の控えと区別する)
    private static string Title(OrderStatus status) =>
        status switch
        {
            OrderStatus.Completed => "受注票 (お渡し済み)",
            OrderStatus.Cancelled => "受注票 (キャンセル)",
            _ => "受注票"
        };

    // お客様に向けた言い方にする (未完了はこれから差し引く額、完了は差し引いた額)
    private static string DepositLabel(OrderStatus status) =>
        status switch
        {
            OrderStatus.Completed => "お会計で差し引いた額",
            OrderStatus.Cancelled => "残り",
            _ => "お会計で差し引く額"
        };

    private static string DepositMethod(OrderDepositType type, string methodName, string? reference) =>
        $"{type.ToDisplayName()} {methodName}{(String.IsNullOrEmpty(reference) ? String.Empty : $" ({reference})")}";

    private static void FillOptional(TemplateSheet sheet, string key, string? value) =>
        ReportText.FillRows(sheet, key, String.IsNullOrEmpty(value) ? [] : [new Dictionary<string, string?> { [key] = value }]);
}
