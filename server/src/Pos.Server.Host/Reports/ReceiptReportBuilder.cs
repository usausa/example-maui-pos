namespace Pos.Server.Host.Reports;

using OysterReport;

using Pos.Server.Host.Infrastructure.Reports;
using Pos.Server.Models.Views;

// レシート PDF (控え・再発行)。Assets/Reports/Receipt.xlsx。項目は端末のレシート (ReceiptTextBuilder) と同じにする
public sealed class ReceiptReportBuilder
{
    private const string TemplatePath = "Assets/Reports/Receipt.xlsx";
    private const string FontPath = "Assets/Fonts/ipaexg.ttf";

    private readonly EmbeddedFontResolver fontResolver = new(FontPath);

    private readonly TimeProvider timeProvider;

    public ReceiptReportBuilder(TimeProvider timeProvider)
    {
        this.timeProvider = timeProvider;
    }

    public byte[] Build(ReceiptReportView report)
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

    private void EditSheet(TemplateSheet sheet, ReceiptReportView report)
    {
        var detail = report.Detail;
        var transaction = detail.Transaction;
        var store = report.Store;

        // 店舗の住所・電話・登録番号は、ないときは行ごと消す
        FillOptional(sheet, "Address", store?.Address);
        FillOptional(sheet, "Phone", String.IsNullOrEmpty(store?.Phone) ? null : "TEL " + store.Phone);
        FillOptional(sheet, "RegistrationNo", String.IsNullOrEmpty(store?.RegistrationNo) ? null : "登録番号 " + store.RegistrationNo);
        sheet.ReplacePlaceholders(new Dictionary<string, string?>
        {
            ["Header"] = store?.ReceiptHeader ?? store?.Name ?? String.Empty,
            ["Title"] = transaction.Type == TransactionType.Return ? "【返品】" : transaction.Status == TransactionStatus.Voided ? "【取消済み】" : "領収書",
            ["TransactedAt"] = ReportText.DateTime(transaction.TransactedAt, report.TimeZone),
            ["StaffName"] = "担当: " + report.StaffName,
            ["TerminalName"] = report.TerminalName,
            ["ReceiptNo"] = "No." + transaction.ReceiptNo,
            ["Total"] = Yen(transaction.Total),
            ["Footer"] = store?.ReceiptFooter ?? String.Empty,
            ["IssuedAt"] = "控え (再発行) " + ReportText.DateTime(timeProvider.GetUtcNow().UtcDateTime, report.TimeZone)
        });

        var info = new List<Dictionary<string, string?>>();
        if (detail.Order is not null)
        {
            info.Add(Row("Info", "受注番号", detail.Order.OrderNo));
        }

        ReportText.FillRows(sheet, "InfoLabel", info);

        // 明細: 商品の行に値引とシリアルの行を続ける
        var serials = detail.Serials.ToLookup(static x => x.TransactionLineId, static x => x.SerialNumber);
        var items = new List<Dictionary<string, string?>>();
        foreach (var line in detail.Lines.OrderBy(static x => x.LineNo))
        {
            items.Add(new Dictionary<string, string?>
            {
                ["ItemLabel"] = line.ProductName,
                ["ItemDetail"] = line.Quantity == 1m ? String.Empty : $"{Yen(line.UnitPrice)} × {ReportText.Quantity(line.Quantity)}",
                ["ItemAmount"] = Yen(line.Amount)
            });
            var discount = line.DiscountAmount + line.AllocatedDiscountAmount;
            if (discount != 0m)
            {
                items.Add(new Dictionary<string, string?> { ["ItemLabel"] = "　値引", ["ItemDetail"] = String.Empty, ["ItemAmount"] = "-" + Yen(discount) });
            }

            foreach (var serial in serials[line.Id])
            {
                items.Add(new Dictionary<string, string?> { ["ItemLabel"] = "　S/N " + serial, ["ItemDetail"] = String.Empty, ["ItemAmount"] = String.Empty });
            }
        }

        ReportText.FillRows(sheet, "ItemLabel", items);

        var sums = new List<Dictionary<string, string?>> { Row("Sum", "小計", Yen(transaction.Subtotal)) };
        if (transaction.DiscountTotal != 0m)
        {
            sums.Add(Row("Sum", "値引", "-" + Yen(transaction.DiscountTotal)));
        }

        sums.AddRange(detail.TaxSummaries.Select(static x => Row("Sum", $"{(x.TaxIncluded ? "内税" : "外税")} {ReportText.Percent(x.Rate)} 対象 {Yen(x.TaxableAmount)}", "税 " + Yen(x.TaxAmount))));
        ReportText.FillRows(sheet, "SumLabel", sums);

        var payments = detail.Payments
            .OrderBy(static x => x.SeqNo)
            .Select(x => Row("Pay", report.PaymentMethodNames.GetValueOrDefault(x.PaymentMethodId) ?? x.Kind.ToDisplayName(), Yen(x.TenderedAmount)))
            .ToList();
        if (transaction.ChangeAmount != 0m)
        {
            payments.Add(Row("Pay", "お釣り", Yen(transaction.ChangeAmount)));
        }

        ReportText.FillRows(sheet, "PayLabel", payments);

        var extras = new List<Dictionary<string, string?>>();
        if ((transaction.PointsEarned != 0) || (transaction.PointsRedeemed != 0))
        {
            extras.Add(Row("Extra", "ポイント利用", ReportText.Count(transaction.PointsRedeemed)));
            extras.Add(Row("Extra", "ポイント付与", ReportText.Count(transaction.PointsEarned)));
            if (transaction.PointsBalanceAfter is not null)
            {
                extras.Add(Row("Extra", "ポイント残高", ReportText.Count(transaction.PointsBalanceAfter.Value)));
            }
        }

        if (detail.Delivery is not null)
        {
            extras.Add(Row("Extra", "配送先: " + detail.Delivery.RecipientName, String.Empty));
            extras.Add(Row("Extra", detail.Delivery.Address, String.Empty));
        }

        ReportText.FillRows(sheet, "ExtraLabel", extras);
    }

    private static string Yen(decimal value) => "¥" + ReportText.Yen(value);

    // 見出しと値の 1 行 (プレースホルダは {prefix}Label / {prefix}Value)
    private static Dictionary<string, string?> Row(string prefix, string label, string value) =>
        new()
        {
            [prefix + "Label"] = label,
            [prefix + "Value"] = value
        };

    private static void FillOptional(TemplateSheet sheet, string key, string? value) =>
        ReportText.FillRows(sheet, key, String.IsNullOrEmpty(value) ? [] : [new Dictionary<string, string?> { [key] = value }]);
}
