namespace Pos.Terminal.Modules;

using Pos.Terminal.Input;

public static class PopupNavigatorExtensions
{
    public static ValueTask<string?> InputNumberAsync(this IPopupNavigator popupNavigator, string title, string value, int maxLength)
    {
        return FocusHelper.WithRestoreFocus(() =>
            popupNavigator.PopupAsync<NumberInputParameter, string?>(
                DialogId.InputNumber,
                new NumberInputParameter(title, value, maxLength)));
    }

    // 番号 (電話・郵便番号・コード・伝票番号) の電卓入力。null = キャンセル、空 = クリア
    public static ValueTask<string?> InputDigitsAsync(this IPopupNavigator popupNavigator, string title, string? value, int maxLength)
    {
        return FocusHelper.WithRestoreFocus(() =>
            popupNavigator.PopupAsync<NumberInputParameter, string?>(
                DialogId.InputNumber,
                new NumberInputParameter(title, value ?? string.Empty, maxLength, digits: true)));
    }
}
