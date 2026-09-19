namespace Pos.Terminal.Modules.Dialogs;

// 確認と情報のダイアログを下端のシート (Confirm / Message) で出し、それ以外 (トースト・ローディングなど) は MauiComponents の実装に委ねる
public sealed class SheetDialog : IDialog
{
    private readonly DialogImplementation inner;

    private readonly IPopupNavigator popupNavigator;

    public SheetDialog(
        DialogImplementation inner,
        IPopupNavigator popupNavigator)
    {
        this.inner = inner;
        this.popupNavigator = popupNavigator;
    }

    public ValueTask InformationAsync(string message, string? title = null, string ok = "OK") =>
        popupNavigator.PopupAsync(DialogId.Message, new MessageParameter(message, title, ok));

    public ValueTask<bool> ConfirmAsync(string message, string? title = null, string ok = "OK", string cancel = "Cancel", bool defaultPositive = false) =>
        popupNavigator.PopupAsync<ConfirmParameter, bool>(DialogId.Confirm, new ConfirmParameter(message, title, ok, cancel));

    public ValueTask<Confirm3Result> Confirm3Async(string message, string? title = null, string ok = "Yes", string cancel = "No", string neutral = "Maybe", bool defaultPositive = false) =>
        inner.Confirm3Async(message, title, ok, cancel, neutral, defaultPositive);

    public ValueTask<int> SelectAsync(string[] items, int selected = -1, string? title = null, string? cancel = null) =>
        inner.SelectAsync(items, selected, title, cancel);

    public ValueTask<PromptResult> PromptAsync(string? defaultValue = null, string? message = null, string? title = null, string ok = "OK", string cancel = "Cancel", string? placeHolder = null, PromptParameter? parameter = null) =>
        inner.PromptAsync(defaultValue, message, title, ok, cancel, placeHolder, parameter);

    public IDisposable Indicator() => inner.Indicator();

    public IDisposable Lock() => inner.Lock();

    public ILoading Loading(string text = "") => inner.Loading(text);

    public MauiComponents.IProgress Progress() => inner.Progress();

    public void Snackbar(string message, int duration = 1000, Color? color = null, Color? textColor = null) =>
        inner.Snackbar(message, duration, color, textColor);

    public ValueTask Toast(string text, bool longDuration = false, double textSize = 14) =>
        inner.Toast(text, longDuration, textSize);
}
