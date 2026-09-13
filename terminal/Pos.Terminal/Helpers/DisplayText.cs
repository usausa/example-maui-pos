namespace Pos.Terminal.Helpers;

// 画面表示用の書式 (サーバ管理画面の DisplayText と同じ表現)
public static class DisplayText
{
    public static string Yen(decimal value) =>
        value < 0 ? "-¥" + (-value).ToString("#,##0", CultureInfo.InvariantCulture) : "¥" + value.ToString("#,##0", CultureInfo.InvariantCulture);

    public static string Quantity(decimal value) => value.ToString("#,##0.##", CultureInfo.InvariantCulture);

    public static string Points(int value) => value.ToString("#,##0", CultureInfo.InvariantCulture) + " pt";

    public static string Percent(decimal rate) => (rate * 100).ToString("0.#", CultureInfo.InvariantCulture) + "%";

    public static string Date(DateOnly value) => value.ToString("yyyy/MM/dd", CultureInfo.InvariantCulture);

    public static string DateTime(DateTime value) => value.ToLocalTime().ToString("yyyy/MM/dd HH:mm", CultureInfo.InvariantCulture);

    public static string Time(DateTime value) => value.ToLocalTime().ToString("HH:mm", CultureInfo.InvariantCulture);

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

    public static string Name(CashEventType value) => value switch
    {
        CashEventType.PaidIn => "入金",
        CashEventType.PaidOut => "出金",
        CashEventType.NoSale => "ドロワ開",
        _ => value.ToString()
    };

    public static string Name(PointHistoryType value) => value switch
    {
        PointHistoryType.Earn => "付与",
        PointHistoryType.Redeem => "利用",
        PointHistoryType.Revoke => "付与取消",
        PointHistoryType.Refund => "返還",
        PointHistoryType.Void => "取消",
        PointHistoryType.Adjust => "調整",
        _ => value.ToString()
    };

    public static string Name(PaymentKind value) => value switch
    {
        PaymentKind.Cash => "現金",
        PaymentKind.Card => "カード",
        PaymentKind.Qr => "QR",
        PaymentKind.EMoney => "電子マネー",
        PaymentKind.Voucher => "商品券",
        PaymentKind.Points => "ポイント",
        PaymentKind.Credit => "掛売",
        PaymentKind.Other => "その他",
        _ => value.ToString()
    };

    public static string Name(StaffRole value) => value switch
    {
        StaffRole.Cashier => "レジ担当",
        StaffRole.Manager => "店長",
        StaffRole.Admin => "管理者",
        _ => value.ToString()
    };
}
