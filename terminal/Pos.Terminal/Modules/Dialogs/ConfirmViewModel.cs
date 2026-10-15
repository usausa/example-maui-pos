namespace Pos.Terminal.Modules.Dialogs;

public sealed record ConfirmParameter(string Message, string? Title, string Ok, string Cancel);

// 確認: IDialog.ConfirmAsync から SheetDialog が開くシート (確定なら true を返す)
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

    public IObserveCommand CloseCommand { get; }

    public IObserveCommand CommitCommand { get; }

    public ConfirmViewModel()
    {
        CloseCommand = MakeAsyncCommand(async () => await CloseAsync());
        CommitCommand = MakeAsyncCommand(async () => await CloseAsync(true));
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
