namespace Pos.Terminal.Modules.Dialogs;

public sealed record ConfirmParameter(string Message, string? Title, string Ok, string Cancel);

// 確認のダイアログ (シート)。IDialog.ConfirmAsync から SheetDialog 経由で開き、確定なら true を返す
public sealed partial class ConfirmViewModel : AppDialogViewModelBase, IPopupInitialize<ConfirmParameter>
{
    [ObservableProperty]
    public partial string Title { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool HasTitle { get; set; }

    [ObservableProperty]
    public partial string Message { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string OkText { get; set; } = "OK";

    [ObservableProperty]
    public partial string CancelText { get; set; } = "キャンセル";

    public IObserveCommand OkCommand { get; }

    public IObserveCommand CancelCommand { get; }

    public ConfirmViewModel(IPopupNavigator popupNavigator)
    {
        OkCommand = MakeAsyncCommand(async () => await popupNavigator.CloseAsync(true));
        CancelCommand = MakeAsyncCommand(async () => await popupNavigator.CloseAsync(false));
    }

    public void Initialize(ConfirmParameter parameter)
    {
        Title = parameter.Title ?? string.Empty;
        HasTitle = !String.IsNullOrEmpty(parameter.Title);
        Message = parameter.Message;
        OkText = parameter.Ok;
        CancelText = parameter.Cancel;
    }
}
