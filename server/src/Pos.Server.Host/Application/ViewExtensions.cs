namespace Pos.Server.Host.Application;

using Pos.Server.Models.Entity;
using Pos.Server.Models.Views;

// 管理画面の表示文字列 (金額・日時の書式、列挙型の日本語名)
public static class ViewExtensions
{
    //--------------------------------------------------------------------------------
    // Format
    //--------------------------------------------------------------------------------

    public static string ToYen(this decimal value) => value.ToString("#,##0", CultureInfo.InvariantCulture);

    public static string ToYen(this decimal? value) => value is null ? "-" : value.Value.ToYen();

    public static string ToYen(this int value) => value.ToString("#,##0", CultureInfo.InvariantCulture);

    public static string ToQuantityText(this decimal value) => value.ToString("#,##0.##", CultureInfo.InvariantCulture);

    public static string ToQuantityText(this decimal? value) => value is null ? "-" : value.Value.ToQuantityText();

    // ポイント数・件数 (3 桁区切り)
    public static string ToPointText(this int value) => value.ToString("N0", CultureInfo.InvariantCulture);

    public static string ToTerminalNoText(this int value) => value.ToString("00", CultureInfo.InvariantCulture);

    // 進捗 (0〜1 を %)
    public static string ToProgressText(this double ratio) => (ratio * 100).ToString("F0", CultureInfo.InvariantCulture) + "%";

    public static string ToPercentText(this decimal rate) => (rate * 100).ToString("0.#", CultureInfo.InvariantCulture) + "%";

    public static string ToDateText(this DateOnly value) => value.ToString("yyyy/MM/dd", CultureInfo.InvariantCulture);

    public static string ToDateText(this DateOnly? value) => value is null ? "-" : value.Value.ToDateText();

    // UTC をサーバのローカル時刻で表示
    public static string ToDateTimeText(this DateTime utc) => DateTime.SpecifyKind(utc, DateTimeKind.Utc).ToLocalTime().ToString("yyyy/MM/dd HH:mm", CultureInfo.InvariantCulture);

    public static string ToDateTimeText(this DateTime? utc) => utc is null ? "-" : utc.Value.ToDateTimeText();

    // 一覧の日時 (営業日の列があるので年を省く)
    public static string ToShortDateTimeText(this DateTime utc) => DateTime.SpecifyKind(utc, DateTimeKind.Utc).ToLocalTime().ToString("MM/dd HH:mm", CultureInfo.InvariantCulture);

    public static string ToShortDateTimeText(this DateTime? utc) => utc is null ? "-" : utc.Value.ToShortDateTimeText();

    public static string ToTimeText(this DateTime utc) => DateTime.SpecifyKind(utc, DateTimeKind.Utc).ToLocalTime().ToString("HH:mm", CultureInfo.InvariantCulture);

    public static string ToTimeText(this DateTime? utc) => utc is null ? "-" : utc.Value.ToTimeText();

    // 符号付き (ポイントの増減など)
    public static string ToDeltaText(this int value) => value.ToString("+#,##0;-#,##0;0", CultureInfo.InvariantCulture);

    public static string ToDeltaText(this decimal value) => value.ToString("+#,##0.##;-#,##0.##;0", CultureInfo.InvariantCulture);

    public static string ToYesNo(this bool value) => value ? "○" : "-";

    public static string ToTaxIncludedText(this bool taxIncluded) => taxIncluded ? "内税" : "外税";

    // 税率別のときだけ課税対象 / 税額、それ以外は税
    public static string ToTaxText(this SalesSummaryView row, SalesSummaryGroupBy groupBy)
    {
        return groupBy == SalesSummaryGroupBy.TaxRate ? $"{row.TaxableAmount.ToYen()} / {row.TaxAmount.ToYen()}" : row.TaxTotal.ToYen();
    }

    // コードと名称
    public static string ToDisplayText(this ProductEntity product)
    {
        return $"{product.Code} {product.Name}";
    }

    public static string ToDisplayText(this CustomerEntity customer)
    {
        return $"{customer.Code} {customer.Name}";
    }

    // 受注・入荷・移動の明細の要約 (先頭の商品名と残りの点数)
    public static string ToLineSummary(this OrderDetailView order) =>
        LineSummary(order.Lines.Count == 0 ? null : order.Lines[0].ProductName, order.Lines.Count);

    public static string ToLineSummary(this InventoryReceiptDetailView receipt) =>
        LineSummary(receipt.Lines.Count == 0 ? null : receipt.Lines[0].ProductName, receipt.Lines.Count);

    public static string ToLineSummary(this InventoryTransferDetailView transfer) =>
        LineSummary(transfer.Lines.Count == 0 ? null : transfer.Lines[0].ProductName, transfer.Lines.Count);

    public static string ToProductText(this InventoryLevelDetailView level)
    {
        return $"{level.ProductCode} {level.ProductName}";
    }

    // セレクタ用 (中分類は字下げ)
    public static string ToIndentedName(this CategoryEntity category)
    {
        return category.ParentId is null ? category.Name : "　" + category.Name;
    }

    //--------------------------------------------------------------------------------
    // Name
    //--------------------------------------------------------------------------------

    public static string ToDisplayName(this TransactionType value) => value switch
    {
        TransactionType.Sale => "販売",
        TransactionType.Return => "返品",
        _ => value.ToString()
    };

    public static string ToDisplayName(this TransactionStatus value) => value switch
    {
        TransactionStatus.Completed => "完了",
        TransactionStatus.Voided => "取消",
        _ => value.ToString()
    };

    public static string ToDisplayName(this ShiftStatus value) => value switch
    {
        ShiftStatus.Open => "開設中",
        ShiftStatus.Closed => "精算済み",
        _ => value.ToString()
    };

    public static string ToDisplayName(this ProductKind value) => value switch
    {
        ProductKind.Goods => "商品",
        ProductKind.Service => "サービス",
        _ => value.ToString()
    };

    public static string ToDisplayName(this PaymentKind value) => value switch
    {
        PaymentKind.Cash => "現金",
        PaymentKind.Card => "カード",
        PaymentKind.Qr => "QR 決済",
        PaymentKind.EMoney => "電子マネー",
        PaymentKind.Voucher => "商品券",
        PaymentKind.Points => "ポイント",
        PaymentKind.Credit => "掛売",
        PaymentKind.Other => "その他",
        _ => value.ToString()
    };

    public static string ToDisplayName(this DiscountType value) => value switch
    {
        DiscountType.Amount => "定額",
        DiscountType.Percent => "率",
        _ => value.ToString()
    };

    public static string ToDisplayName(this DiscountScope value) => value switch
    {
        DiscountScope.Line => "明細",
        DiscountScope.Transaction => "取引",
        _ => value.ToString()
    };

    public static string ToDisplayName(this TaxKind value) => value switch
    {
        TaxKind.Standard => "標準",
        TaxKind.Reduced => "軽減",
        TaxKind.Exempt => "非課税",
        _ => value.ToString()
    };

    public static string ToDisplayName(this AccountRole value) => value switch
    {
        AccountRole.Administrator => "管理者",
        AccountRole.Operator => "オペレーター",
        _ => value.ToString()
    };

    public static string ToDisplayName(this StaffRole value) => value switch
    {
        StaffRole.Admin => "管理者",
        StaffRole.Manager => "店長",
        StaffRole.Cashier => "レジ担当",
        _ => value.ToString()
    };

    public static string ToDisplayName(this CashEventType value) => value switch
    {
        CashEventType.PaidIn => "入金",
        CashEventType.PaidOut => "出金",
        CashEventType.NoSale => "ドロワ開",
        _ => value.ToString()
    };

    public static string ToDisplayName(this InventoryChangeType value) => value switch
    {
        InventoryChangeType.Sale => "販売",
        InventoryChangeType.Return => "返品",
        InventoryChangeType.Void => "取消",
        InventoryChangeType.PhysicalCount => "棚卸",
        InventoryChangeType.Adjustment => "調整",
        InventoryChangeType.Receive => "入荷",
        InventoryChangeType.TransferOut => "移動出荷",
        InventoryChangeType.TransferIn => "移動受領",
        _ => value.ToString()
    };

    public static string ToDisplayName(this PointHistoryType value) => value switch
    {
        PointHistoryType.Earn => "付与",
        PointHistoryType.Redeem => "利用",
        PointHistoryType.Refund => "返還",
        PointHistoryType.Revoke => "付与取消",
        PointHistoryType.Void => "取消",
        PointHistoryType.Adjust => "調整",
        _ => value.ToString()
    };

    public static string ToDisplayName(this TaxRounding value) => value switch
    {
        TaxRounding.Floor => "切り捨て",
        TaxRounding.Round => "四捨五入",
        TaxRounding.Ceiling => "切り上げ",
        _ => value.ToString()
    };

    public static string ToDisplayName(this PointBasis value) => value switch
    {
        PointBasis.TaxIncluded => "税込",
        PointBasis.TaxExcluded => "税抜",
        _ => value.ToString()
    };

    public static string ToDisplayName(this SalesSummaryGroupBy value) => value switch
    {
        SalesSummaryGroupBy.Day => "日別",
        SalesSummaryGroupBy.Store => "店舗別",
        SalesSummaryGroupBy.Hour => "時間帯別",
        SalesSummaryGroupBy.Terminal => "端末別",
        SalesSummaryGroupBy.Staff => "担当別",
        SalesSummaryGroupBy.PaymentMethod => "支払方法別",
        SalesSummaryGroupBy.TaxRate => "税率別",
        SalesSummaryGroupBy.Category => "部門別",
        _ => value.ToString()
    };

    public static string ToDisplayName(this ProductSalesSort value) => value switch
    {
        ProductSalesSort.NetSales => "純売上順",
        ProductSalesSort.Quantity => "数量順",
        _ => value.ToString()
    };

    //--------------------------------------------------------------------------------
    // Helper
    //--------------------------------------------------------------------------------

    private static string LineSummary(string? firstProductName, int count) =>
        firstProductName is null ? "-" : $"{firstProductName}{(count > 1 ? $" 他 {count - 1} 点" : string.Empty)}";
}
