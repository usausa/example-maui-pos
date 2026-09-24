namespace Pos.Server.Host.Endpoints;

using Pos.Server.Host.Models.Export;
using Pos.Server.Services;

// 業務ルール違反と警告の文言
public static class ApiRuleText
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
        _ => reason.ToString()
    };

    public static string Of(WarningCode code) => code switch
    {
        WarningCode.PointBalanceNegative => "ポイント残高が不足しています",
        WarningCode.ProductInactive => "販売停止中の商品です",
        WarningCode.InventoryNegative => "在庫がマイナスになります",
        WarningCode.DayAlreadyClosed => "締め済みの営業日の取引です (締め直すまで日計に含まれません)",
        _ => code.ToString()
    };

    // 商品 CSV の取込の誤り (列は CSV の見出しで示す)
    public static string Of(ProductImportError error)
    {
        var header = HeaderOf(error.Column);
        return error.Problem switch
        {
            ProductImportProblem.Required => $"「{header}」を入力してください",
            ProductImportProblem.TooLong => $"「{header}」は {MaxLengthOf(error.Column)} 文字以内にしてください",
            ProductImportProblem.Invalid => error.Column switch
            {
                ProductImportColumn.Kind => $"「{header}」は Goods (物品) か Service (サービス) にしてください",
                ProductImportColumn.Price or ProductImportColumn.Cost => $"「{header}」は 0 以上の数値にしてください",
                ProductImportColumn.PointRate => $"「{header}」は 0〜1 の数値にしてください (0.1 = 10%)",
                _ => $"「{header}」は True か False にしてください"
            },
            ProductImportProblem.NotFound => $"「{header}」に該当する{(error.Column == ProductImportColumn.CategoryCode ? "部門" : "税率")}がありません",
            ProductImportProblem.Duplicated => $"「{header}」がファイルの中で重複しています",
            ProductImportProblem.InUse => error.Column == ProductImportColumn.Code
                ? $"「{header}」は削除済みの商品で使われています"
                : $"「{header}」は他の商品 (削除済みを含む) で使われています",
            _ => error.Problem.ToString()
        };
    }

    private static string HeaderOf(ProductImportColumn column) => column switch
    {
        ProductImportColumn.Code => ProductCsvHeader.Code,
        ProductImportColumn.Barcode => ProductCsvHeader.Barcode,
        ProductImportColumn.Name => ProductCsvHeader.Name,
        ProductImportColumn.Kana => ProductCsvHeader.Kana,
        ProductImportColumn.Brand => ProductCsvHeader.Brand,
        ProductImportColumn.ModelNo => ProductCsvHeader.ModelNo,
        ProductImportColumn.CategoryCode => ProductCsvHeader.CategoryCode,
        ProductImportColumn.Kind => ProductCsvHeader.Kind,
        ProductImportColumn.Price => ProductCsvHeader.Price,
        ProductImportColumn.TaxIncluded => ProductCsvHeader.TaxIncluded,
        ProductImportColumn.TaxRateCode => ProductCsvHeader.TaxRateCode,
        ProductImportColumn.Cost => ProductCsvHeader.Cost,
        ProductImportColumn.PointRate => ProductCsvHeader.PointRate,
        ProductImportColumn.RequiresSerial => ProductCsvHeader.RequiresSerial,
        ProductImportColumn.TrackInventory => ProductCsvHeader.TrackInventory,
        ProductImportColumn.AllowsPriceOverride => ProductCsvHeader.AllowsPriceOverride,
        ProductImportColumn.Unit => ProductCsvHeader.Unit,
        ProductImportColumn.IsActive => ProductCsvHeader.IsActive,
        _ => column.ToString()
    };

    private static int MaxLengthOf(ProductImportColumn column) => column switch
    {
        ProductImportColumn.Barcode => Length.Barcode,
        ProductImportColumn.Name => Length.Name,
        ProductImportColumn.Kana => Length.Kana,
        ProductImportColumn.Brand => Length.Brand,
        ProductImportColumn.ModelNo => Length.ModelNo,
        ProductImportColumn.TaxRateCode => Length.TaxRateCode,
        ProductImportColumn.Unit => Length.Unit,
        _ => Length.Code
    };
}
