namespace Pos.Terminal.Models;

// 業務ルール違反と警告の文言
public static class RuleText
{
    public static string Of(RuleReason reason) => reason switch
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
        _ => reason.ToString()
    };

    public static string Of(WarningCode code) => code switch
    {
        WarningCode.PointBalanceNegative => "ポイント残高が不足しています",
        WarningCode.ProductInactive => "販売停止中の商品です",
        WarningCode.InventoryNegative => "在庫がマイナスになります",
        _ => code.ToString()
    };
}
