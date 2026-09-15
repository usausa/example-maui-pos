namespace Pos.Domain;

// 文字列項目の長さ。DB の列長・Request の検証・管理画面のフォーム・端末の入力桁数で同じ値を使う
public static class Length
{
    // 共通
    public const int Code = 20;
    public const int Name = 100;
    public const int Kana = 100;
    public const int Phone = 20;
    public const int Email = 100;
    public const int PostalCode = 10;
    public const int Address = 200;

    // 備考 (会員・シフト・取引は長め、明細・支払・配送先は短め)
    public const int Note = 500;
    public const int LineNote = 200;

    // 理由 (値引・取消・ポイント調整・在庫調整)。入出金は短め
    public const int Reason = 200;
    public const int CashEventReason = 100;

    // 店舗
    public const int StoreCode = 10;
    public const int RegistrationNo = 14;
    public const int ReceiptText = 500;
    public const int TimeZone = 50;

    // 短い名称のマスタ
    public const int TerminalName = 50;
    public const int StaffName = 50;
    public const int TaxRateCode = 10;
    public const int TaxRateName = 50;
    public const int DiscountName = 50;
    public const int PaymentMethodName = 50;
    public const int PaymentMethodShortName = 10;
    public const int AdjustmentReasonName = 50;

    // 商品
    public const int Barcode = 20;
    public const int Brand = 50;
    public const int ModelNo = 50;
    public const int Unit = 10;
    public const int ImageUrl = 500;

    // 取引
    public const int ReceiptNo = 20;
    public const int Reference = 50;
    public const int TimeSlot = 20;

    // 設定
    public const int CompanyName = 100;
    public const int Currency = 3;
    public const int BusinessDayStartTime = 5;

    // 端末の電卓入力の桁数
    public const int PhoneDigits = 13;
    public const int PostalCodeDigits = 7;
    public const int BirthDateDigits = 8;
    public const int BarcodeDigits = 13;
    public const int ReferenceDigits = 12;
    public const int QuantityDigits = 4;
    public const int AmountDigits = 8;
    public const int CashDigits = 9;
    public const int PointsDigits = 7;
    public const int CountDigits = 4;
    public const int StockDigits = 6;
    public const int DiscountValueDigits = 7;
    public const int ReceiptNoDigits = 8;
}
