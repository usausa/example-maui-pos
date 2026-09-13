namespace Pos.Terminal.Helpers;

// IDialog の日本語既定ボタン
public static class AppDialogExtensions
{
    public static ValueTask<bool> AskAsync(this IDialog dialog, string message, string? title = null, string ok = "OK") =>
        dialog.ConfirmAsync(message, title, ok, "キャンセル");

    public static ValueTask<int> ChooseAsync(this IDialog dialog, string[] items, string? title = null, int selected = -1) =>
        dialog.SelectAsync(items, selected, title, "キャンセル");

    public static ValueTask<T?> ChooseAsync<T>(this IDialog dialog, IList<T> items, Func<T, string> formatter, string? title = null, int selected = -1) =>
        dialog.SelectAsync(items, formatter, selected, title, "キャンセル");

    public static ValueTask<PromptResult> InputAsync(this IDialog dialog, string title, string? defaultValue = null, string? placeHolder = null, PromptParameter? parameter = null) =>
        dialog.PromptAsync(defaultValue, null, title, "OK", "キャンセル", placeHolder, parameter);
}
