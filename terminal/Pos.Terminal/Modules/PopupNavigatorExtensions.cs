namespace Pos.Terminal.Modules;

// 電卓入力を種類ごとに用意する (表題と桁数は画面側で持たない)。null = キャンセル
public static class PopupNavigatorExtensions
{
    //--------------------------------------------------------------------------------
    // 番号 (先頭の 0 を残し、空 = クリア)
    //--------------------------------------------------------------------------------

    public static ValueTask<string?> InputPhoneAsync(this IPopupNavigator popupNavigator, string? value) =>
        popupNavigator.InputDigitsAsync("電話番号", value, Length.PhoneDigits);

    public static ValueTask<string?> InputPostalCodeAsync(this IPopupNavigator popupNavigator, string? value) =>
        popupNavigator.InputDigitsAsync("郵便番号", value, Length.PostalCodeDigits);

    public static ValueTask<string?> InputBirthDateAsync(this IPopupNavigator popupNavigator, string? value) =>
        popupNavigator.InputDigitsAsync("生年月日 (yyyyMMdd)", value, Length.BirthDateDigits);

    // 会員番号または電話番号 (検索)
    public static ValueTask<string?> InputCustomerNoAsync(this IPopupNavigator popupNavigator) =>
        popupNavigator.InputDigitsAsync("会員番号 / 電話番号", null, Length.PhoneDigits);

    // 商品コードまたは JAN
    public static ValueTask<string?> InputProductCodeAsync(this IPopupNavigator popupNavigator) =>
        popupNavigator.InputDigitsAsync("コード / JAN", null, Length.BarcodeDigits);

    // レシート番号の端末番号 (2 桁) と連番 (6 桁)
    public static ValueTask<string?> InputReceiptNoAsync(this IPopupNavigator popupNavigator) =>
        popupNavigator.InputDigitsAsync("レシート番号 (端末 2 桁 + 連番 6 桁)", null, Length.ReceiptNoDigits);

    // 支払の伝票番号
    public static ValueTask<string?> InputReferenceAsync(this IPopupNavigator popupNavigator, string methodName) =>
        popupNavigator.InputDigitsAsync($"{methodName} の伝票番号", null, Length.ReferenceDigits);

    //--------------------------------------------------------------------------------
    // 数値
    //--------------------------------------------------------------------------------

    public static ValueTask<string?> InputQuantityAsync(this IPopupNavigator popupNavigator, string title, decimal value) =>
        popupNavigator.InputNumberAsync(title, ViewHelper.Quantity(value), Length.QuantityDigits);

    // 単価・入出金・釣銭準備金
    public static ValueTask<string?> InputAmountAsync(this IPopupNavigator popupNavigator, string title, decimal value) =>
        popupNavigator.InputNumberAsync(title, Number(value), Length.AmountDigits);

    // 実査金額 (金種計の合計になるので桁が多い)
    public static ValueTask<string?> InputCashAsync(this IPopupNavigator popupNavigator, string title, decimal value) =>
        popupNavigator.InputNumberAsync(title, Number(value), Length.CashDigits);

    public static ValueTask<string?> InputPointsAsync(this IPopupNavigator popupNavigator, string title, int value) =>
        popupNavigator.InputNumberAsync(title, Number(value), Length.PointsDigits);

    // 金種の枚数
    public static ValueTask<string?> InputCountAsync(this IPopupNavigator popupNavigator, string title, string value) =>
        popupNavigator.InputNumberAsync(title, value, Length.CountDigits);

    // 棚卸の実数・調整の増減
    public static ValueTask<string?> InputStockAsync(this IPopupNavigator popupNavigator, string title, decimal value) =>
        popupNavigator.InputNumberAsync(title, ViewHelper.Quantity(value), Length.StockDigits);

    // 値引の額または率
    public static ValueTask<string?> InputDiscountValueAsync(this IPopupNavigator popupNavigator, bool isAmount, string? value) =>
        popupNavigator.InputNumberAsync(isAmount ? "値引額 (¥)" : "値引率 (%)", value ?? "0", Length.DiscountValueDigits);

    //--------------------------------------------------------------------------------
    // Helper
    //--------------------------------------------------------------------------------

    private static string Number(decimal value) => value.ToString("0", CultureInfo.InvariantCulture);

    private static ValueTask<string?> InputNumberAsync(this IPopupNavigator popupNavigator, string title, string value, int maxLength) =>
        popupNavigator.PopupAsync<NumberInputParameter, string?>(
            DialogId.InputNumber,
            new NumberInputParameter(title, value, maxLength));

    private static ValueTask<string?> InputDigitsAsync(this IPopupNavigator popupNavigator, string title, string? value, int maxLength) =>
        popupNavigator.PopupAsync<NumberInputParameter, string?>(
            DialogId.InputNumber,
            new NumberInputParameter(title, value ?? string.Empty, maxLength, digits: true));
}
