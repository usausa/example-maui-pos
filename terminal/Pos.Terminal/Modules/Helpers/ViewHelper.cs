namespace Pos.Terminal.Modules.Helpers;

using Pos.Terminal.Models.Entity;

// 画面表示用の書式、列挙型の文言、業務ルールの文言 (XAML からは DisplayNameConverter で使う)
public static class ViewHelper
{
    // StateContainer の状態 (結果を見せるときは空文字)
    public const string LoadingState = "Loading";

    public const string OfflineState = "Offline";

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

    // 明細の要約 (先頭の商品名と残りの点数)
    public static string LineSummary(string? firstProductName, int count) =>
        firstProductName is null ? string.Empty : $"{firstProductName}{(count > 1 ? $" 他 {count - 1} 点" : string.Empty)}";

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

    public static string Name(OrderStatus value) => value switch
    {
        OrderStatus.Ordered => "入荷待ち",
        OrderStatus.Arrived => "引き渡し待ち",
        OrderStatus.Completed => "完了",
        OrderStatus.Cancelled => "キャンセル",
        _ => value.ToString()
    };

    public static string Name(OrderType value) => value switch
    {
        OrderType.BackOrder => "取り寄せ",
        OrderType.Hold => "取り置き",
        _ => value.ToString()
    };

    public static string Name(ReceivingKind value) => value switch
    {
        ReceivingKind.Receipt => "入荷",
        ReceivingKind.Transfer => "移動",
        _ => value.ToString()
    };

    public static string Name(ReceivingLineState value) => value switch
    {
        ReceivingLineState.Unchecked => "未確認",
        ReceivingLineState.Match => "一致",
        ReceivingLineState.Shortage => "不足",
        ReceivingLineState.Excess => "過剰",
        _ => value.ToString()
    };

    // 値引の値 (率なら % 表示)
    public static string DiscountValue(DiscountType type, decimal value) =>
        type == DiscountType.Percent ? Percent(value) : Yen(value);

    //--------------------------------------------------------------------------------
    // Rule
    //--------------------------------------------------------------------------------

    public static string Reason(RuleReason reason) => reason switch
    {
        RuleReason.CalculationMismatch => "計算結果が一致しません",
        RuleReason.CustomerRequiredForPoints => "ポイントの付与・利用には会員の指定が必要です",
        RuleReason.CustomerRequiredForPointRefund => "ポイントの取消・返還には会員の指定が必要です",
        RuleReason.DuplicateReceiptNo => "レシート番号が重複しています",
        RuleReason.HasReturns => "返品済みの取引は取消できません",
        RuleReason.TransactionNotFound => "取引が見つかりません",
        RuleReason.OriginalNotFound => "元取引が見つかりません",
        RuleReason.OriginalNotReturnable => "返品できない取引です",
        RuleReason.PointRefundMismatch => "ポイントの返還額が利用ポイントと一致しません",
        RuleReason.PaymentTotalMismatch => "支払合計が会計金額と一致しません",
        RuleReason.PaymentAmountInvalid => "支払額が不正です",
        RuleReason.RefundTenderedMismatch => "返金では預り額と返金額を同じにしてください",
        RuleReason.RefundTotalMismatch => "返金合計が返品金額と一致しません",
        RuleReason.RefundAmountInvalid => "返金額が不正です",
        RuleReason.ChangeNotAllowed => "釣銭を出せない支払方法です",
        RuleReason.TenderedShort => "預り合計が会計金額に足りません",
        RuleReason.TenderedLessThanAmount => "預り額が支払額より少なくなっています",
        RuleReason.PriceOverrideNotAllowed => "売価を変更できない商品です",
        RuleReason.ProductNotFound => "商品が見つかりません",
        RuleReason.ReturnQuantityExceeded => "返品数量が返品可能な数量を超えています",
        RuleReason.ShiftClosed => "シフトが精算済みです",
        RuleReason.ShiftClosedForVoid => "精算済みのシフトの取引は取消できません",
        RuleReason.ShiftNotFound => "シフトが見つかりません",
        RuleReason.ShiftTerminalMismatch => "シフトの端末が一致しません",
        RuleReason.DiscountValueInvalid => "値引の値が不正です",
        RuleReason.DiscountLineNotFound => "値引の対象明細が存在しません",
        RuleReason.LineNotInOriginal => "元取引の明細ではありません",
        RuleReason.UnitPriceNegative => "単価は 0 以上を指定してください",
        RuleReason.TransactionDiscountExceeds => "取引値引が値引後の合計を超えています",
        RuleReason.AlreadyVoided => "取消済みの取引です",
        RuleReason.QuantityNotPositive => "数量は正の数を指定してください",
        RuleReason.DuplicateLineId => "明細 ID が重複しています",
        RuleReason.NoLines => "明細がありません",
        RuleReason.LineDiscountExceeds => "明細値引が明細金額を超えています",
        RuleReason.DayClosed => "締め済みの営業日の取引は取消できません",
        RuleReason.OrderNotFound => "受注が見つかりません",
        RuleReason.OrderNotReady => "引き渡し待ちの受注ではありません",
        RuleReason.OrderNotEditable => "完了・キャンセルした受注は変更できません",
        RuleReason.OrderNotOrdered => "入荷待ちの受注ではありません",
        RuleReason.OrderNotCancellable => "完了・キャンセルした受注はキャンセルできません",
        RuleReason.StoreNotFound => "店舗が見つかりません",
        RuleReason.CustomerNotFound => "会員が見つかりません",
        RuleReason.SupplierNotFound => "仕入先が見つかりません",
        RuleReason.InventoryReceiptNotDraft => "受領・キャンセルした入荷です",
        RuleReason.InventoryTransferNotRequested => "出荷・キャンセルした移動です",
        RuleReason.InventoryTransferNotShipped => "出荷済みの移動ではありません",
        RuleReason.DiscountApprovalRequired => "承認が必要な値引です",
        RuleReason.VoidApprovalRequired => "取消には店長以上の承認が必要です",
        RuleReason.ApproverNotAllowed => "承認者が店長以上の有効なスタッフではありません",
        RuleReason.StaffNotAllowed => "担当がこの店舗の有効なスタッフではありません",
        _ => reason.ToString()
    };

    public static string Warning(WarningCode code) => code switch
    {
        WarningCode.PointBalanceNegative => "ポイント残高が不足しています",
        WarningCode.ProductInactive => "販売停止中の商品です",
        WarningCode.InventoryNegative => "在庫がマイナスになります",
        WarningCode.DayAlreadyClosed => "締め済みの営業日の取引です",
        _ => code.ToString()
    };
}
