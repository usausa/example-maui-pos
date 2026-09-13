namespace Pos.Terminal.Services;

using Pos.Shared.Transactions;

// レシート文字列 (等幅 32 桁)。画面表示と共有に使う
public static class ReceiptFormatter
{
    private const int Width = 32;

    public static string Format(TransactionResponse transaction, StoreResponse? store, string terminalName, string staffName, IReadOnlyDictionary<Guid, string> paymentMethodNames)
    {
        ArgumentNullException.ThrowIfNull(transaction);
        ArgumentNullException.ThrowIfNull(paymentMethodNames);

        var sb = new StringBuilder();
        AppendCenter(sb, store?.ReceiptHeader ?? store?.Name ?? string.Empty);
        if (!String.IsNullOrEmpty(store?.Address))
        {
            AppendCenter(sb, store.Address);
        }

        if (!String.IsNullOrEmpty(store?.Phone))
        {
            AppendCenter(sb, "TEL " + store.Phone);
        }

        if (!String.IsNullOrEmpty(store?.RegistrationNo))
        {
            AppendCenter(sb, "登録番号 " + store.RegistrationNo);
        }

        sb.AppendLine();
        AppendCenter(sb, transaction.Type == TransactionType.Return ? "【返品】" : transaction.Status == TransactionStatus.Voided ? "【取消済み】" : "領収書");
        AppendPair(sb, transaction.TransactedAt.ToLocalTime().ToString("yyyy/MM/dd HH:mm", CultureInfo.InvariantCulture), "担当: " + staffName);
        AppendPair(sb, terminalName, "No." + transaction.ReceiptNo);
        AppendLine(sb, '-');

        foreach (var line in transaction.Lines)
        {
            sb.AppendLine(Truncate(line.ProductName, Width));
            var quantity = line.Quantity == 1m ? string.Empty : $"{Yen(line.UnitPrice)} x {Quantity(line.Quantity)}";
            AppendPair(sb, "  " + quantity, Yen(line.Amount));
            var discount = line.DiscountAmount + line.AllocatedDiscountAmount;
            if (discount != 0m)
            {
                AppendPair(sb, "  値引", "-" + Yen(discount));
            }

            foreach (var serial in line.SerialNumbers)
            {
                sb.AppendLine("  S/N " + serial);
            }
        }

        AppendLine(sb, '-');
        AppendPair(sb, "小計", Yen(transaction.Subtotal));
        if (transaction.DiscountTotal != 0m)
        {
            AppendPair(sb, "値引", "-" + Yen(transaction.DiscountTotal));
        }

        foreach (var tax in transaction.TaxSummaries)
        {
            AppendPair(sb, $"{(tax.TaxIncluded ? "内税" : "外税")}{tax.Rate * 100:0.#}% 対象 {Yen(tax.TaxableAmount)}", "税 " + Yen(tax.TaxAmount));
        }

        AppendPair(sb, "合計", Yen(transaction.Total));
        foreach (var payment in transaction.Payments)
        {
            AppendPair(sb, paymentMethodNames.GetValueOrDefault(payment.PaymentMethodId, payment.Kind.ToString()), Yen(payment.TenderedAmount));
        }

        if (transaction.ChangeAmount != 0m)
        {
            AppendPair(sb, "お釣り", Yen(transaction.ChangeAmount));
        }

        if ((transaction.PointsEarned != 0) || (transaction.PointsRedeemed != 0))
        {
            AppendLine(sb, '-');
            AppendPair(sb, "ポイント利用", transaction.PointsRedeemed.ToString("N0", CultureInfo.InvariantCulture));
            AppendPair(sb, "ポイント付与", transaction.PointsEarned.ToString("N0", CultureInfo.InvariantCulture));
            if (transaction.PointsBalanceAfter is not null)
            {
                AppendPair(sb, "ポイント残高", transaction.PointsBalanceAfter.Value.ToString("N0", CultureInfo.InvariantCulture));
            }
        }

        if (transaction.Delivery is not null)
        {
            AppendLine(sb, '-');
            sb.AppendLine("配送先: " + transaction.Delivery.RecipientName);
            sb.AppendLine(Truncate(transaction.Delivery.Address, Width));
        }

        sb.AppendLine();
        AppendCenter(sb, store?.ReceiptFooter ?? string.Empty);
        return sb.ToString();
    }

    private static string Yen(decimal value) => "¥" + value.ToString("#,##0", CultureInfo.InvariantCulture);

    private static string Quantity(decimal value) => value.ToString("#,##0.##", CultureInfo.InvariantCulture);

    // 全角は 2 桁として幅を数える
    private static int DisplayWidth(string text) => text.Sum(static c => c > 0xFF ? 2 : 1);

    private static string Truncate(string text, int width)
    {
        var sb = new StringBuilder();
        var total = 0;
        foreach (var c in text)
        {
            var w = c > 0xFF ? 2 : 1;
            if (total + w > width)
            {
                break;
            }

            sb.Append(c);
            total += w;
        }

        return sb.ToString();
    }

    private static void AppendCenter(StringBuilder sb, string text)
    {
        if (String.IsNullOrEmpty(text))
        {
            return;
        }

        var value = Truncate(text, Width);
        var padding = Math.Max(0, (Width - DisplayWidth(value)) / 2);
        sb.Append(' ', padding).AppendLine(value);
    }

    private static void AppendPair(StringBuilder sb, string left, string right)
    {
        var l = Truncate(left, Width - DisplayWidth(right) - 1);
        var padding = Math.Max(1, Width - DisplayWidth(l) - DisplayWidth(right));
        sb.Append(l).Append(' ', padding).AppendLine(right);
    }

    private static void AppendLine(StringBuilder sb, char c) => sb.Append(c, Width).AppendLine();
}
