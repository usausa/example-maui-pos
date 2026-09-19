namespace Pos.Terminal.Modules.Dialogs;

public sealed record MessageParameter(string Message, string? Title, string Ok);

// 情報のダイアログ (シート)。IDialog.InformationAsync から SheetDialog 経由で開く
public sealed partial class MessageViewModel : AppDialogViewModelBase, IPopupInitialize<MessageParameter>
{
    [ObservableProperty]
    public partial string Title { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool HasTitle { get; set; }

    [ObservableProperty]
    public partial string Message { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string OkText { get; set; } = "OK";

    public IObserveCommand CloseCommand { get; }

    public MessageViewModel(IPopupNavigator popupNavigator)
    {
        CloseCommand = MakeAsyncCommand(async () => await popupNavigator.CloseAsync());
    }

    public void Initialize(MessageParameter parameter)
    {
        Title = parameter.Title ?? string.Empty;
        HasTitle = !String.IsNullOrEmpty(parameter.Title);
        Message = parameter.Message;
        OkText = parameter.Ok;
    }
}
