namespace Pos.Terminal.Models;

using Pos.Terminal.Models.Entity;

// 画面表示用の書式と列挙型の文言 (XAML からは DisplayNameConverter で使う)
public static class DisplayText
{
    public static string Yen(decimal value) =>
        value < 0 ? "-¥" + (-value).ToString("#,##0", CultureInfo.InvariantCulture) : "¥" + value.ToString("#,##0", CultureInfo.InvariantCulture);

    // 差し引く項目 (返品・出金・値引) の表示。0 のときは符号を付けない
    public static string MinusYen(decimal value) => value == 0 ? Yen(0) : "-" + Yen(value);

    // 過不足 (プラスは + を付ける)
    public static string SignedYen(decimal value) => value == 0 ? "±¥0" : value > 0 ? "+" + Yen(value) : Yen(value);

    public static string Quantity(decimal value) => value.ToString("#,##0.##", CultureInfo.InvariantCulture);

    public static string Points(int value) => value.ToString("#,##0", CultureInfo.InvariantCulture) + " pt";

    public static string Percent(decimal rate) => (rate * 100).ToString("0.#", CultureInfo.InvariantCulture) + "%";

    public static string Date(DateOnly value) => DateTimeHelper.FormatDate(value);

    public static string DateTime(DateTime value) => DateTimeHelper.FormatDateTime(value);

    public static string Time(DateTime value) => DateTimeHelper.FormatTime(value);

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
        ShiftStatus.Closed => "精算済",
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

    public static string Name(OutboxStatus value) => value switch
    {
        OutboxStatus.Pending => "未送信",
        OutboxStatus.Sent => "送信済",
        OutboxStatus.Failed => "要確認",
        _ => value.ToString()
    };

    public static string Name(OutboxKind value) => value switch
    {
        OutboxKind.ShiftOpen => "レジ開設",
        OutboxKind.Transaction => "取引",
        OutboxKind.TransactionVoid => "取引取消",
        OutboxKind.CashEvent => "入出金",
        OutboxKind.ShiftClose => "精算",
        OutboxKind.InventoryChanges => "在庫変動",
        _ => value.ToString()
    };
}
