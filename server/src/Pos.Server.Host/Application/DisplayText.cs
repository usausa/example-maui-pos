namespace Pos.Server.Host.Application;

using Pos.Server.Host.Infrastructure.Reports;

// 管理画面の表示文字列 (列挙型の日本語名、金額・日時の書式)
public static class DisplayText
{
    public static string Yen(decimal value) => value.ToString("#,##0", CultureInfo.InvariantCulture);

    public static string Yen(decimal? value) => value is null ? "-" : Yen(value.Value);

    public static string Quantity(decimal value) => value.ToString("#,##0.##", CultureInfo.InvariantCulture);

    public static string Percent(decimal rate) => (rate * 100).ToString("0.#", CultureInfo.InvariantCulture) + "%";

    public static string Date(DateOnly value) => value.ToString("yyyy/MM/dd", CultureInfo.InvariantCulture);

    public static string Date(DateOnly? value) => value is null ? "-" : Date(value.Value);

    // UTC をサーバのローカル時刻で表示
    public static string DateTime(DateTime utc) => System.DateTime.SpecifyKind(utc, DateTimeKind.Utc).ToLocalTime().ToString("yyyy/MM/dd HH:mm", CultureInfo.InvariantCulture);

    public static string DateTime(DateTime? utc) => utc is null ? "-" : DateTime(utc.Value);

    public static string Time(DateTime utc) => System.DateTime.SpecifyKind(utc, DateTimeKind.Utc).ToLocalTime().ToString("HH:mm", CultureInfo.InvariantCulture);

    public static string Time(DateTime? utc) => utc is null ? "-" : Time(utc.Value);

    // 符号付き (ポイントの増減など)
    public static string Delta(int value) => value.ToString("+#,##0;-#,##0;0", CultureInfo.InvariantCulture);

    public static string YesNo(bool value) => value ? "○" : "-";

    public static string Name(TransactionType value) => value switch
    {
        TransactionType.Sale => "販売",
        TransactionType.Return => "返品",
        _ => value.ToString()
    };

    public static string Name(TransactionStatus value) => value switch
    {
        TransactionStatus.Completed => "完了",
        TransactionStatus.Voided => "取消",
        _ => value.ToString()
    };

    public static string Name(ShiftStatus value) => value switch
    {
        ShiftStatus.Open => "開設中",
        ShiftStatus.Closed => "精算済み",
        _ => value.ToString()
    };

    public static string Name(ProductKind value) => value switch
    {
        ProductKind.Goods => "商品",
        ProductKind.Service => "サービス",
        _ => value.ToString()
    };

    public static string Name(PaymentKind value) => value switch
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

    public static string Name(DiscountType value) => value switch
    {
        DiscountType.Amount => "定額",
        DiscountType.Percent => "率",
        _ => value.ToString()
    };

    public static string Name(DiscountScope value) => value switch
    {
        DiscountScope.Line => "明細",
        DiscountScope.Transaction => "取引",
        _ => value.ToString()
    };

    public static string Name(TaxKind value) => value switch
    {
        TaxKind.Standard => "標準",
        TaxKind.Reduced => "軽減",
        TaxKind.Exempt => "非課税",
        _ => value.ToString()
    };

    public static string Name(StaffRole value) => value switch
    {
        StaffRole.Admin => "管理者",
        StaffRole.Manager => "店長",
        StaffRole.Cashier => "レジ担当",
        _ => value.ToString()
    };

    public static string Name(CashEventType value) => value switch
    {
        CashEventType.PaidIn => "入金",
        CashEventType.PaidOut => "出金",
        CashEventType.NoSale => "ドロワ開",
        _ => value.ToString()
    };

    public static string Name(InventoryChangeType value) => value switch
    {
        InventoryChangeType.Sale => "販売",
        InventoryChangeType.Return => "返品",
        InventoryChangeType.Void => "取消",
        InventoryChangeType.PhysicalCount => "棚卸",
        InventoryChangeType.Adjustment => "調整",
        _ => value.ToString()
    };

    public static string Name(PointHistoryType value) => value switch
    {
        PointHistoryType.Earn => "付与",
        PointHistoryType.Redeem => "利用",
        PointHistoryType.Refund => "返還",
        PointHistoryType.Revoke => "付与取消",
        PointHistoryType.Void => "取消",
        PointHistoryType.Adjust => "調整",
        _ => value.ToString()
    };

    public static string Name(TaxRounding value) => value switch
    {
        TaxRounding.Floor => "切り捨て",
        TaxRounding.Round => "四捨五入",
        TaxRounding.Ceiling => "切り上げ",
        _ => value.ToString()
    };

    public static string Name(PointBasis value) => value switch
    {
        PointBasis.TaxIncluded => "税込",
        PointBasis.TaxExcluded => "税抜",
        _ => value.ToString()
    };

    public static string Name(SalesSummaryGroupBy value) => value switch
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
}
