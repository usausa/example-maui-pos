namespace Pos.Terminal.Modules.Navigation.Modal;

public sealed record ReasonItem(Guid? Id, string Name);

public sealed record ReasonSelectParameter(string Title, IReadOnlyList<ReasonItem> Items, bool AllowCustom);

public sealed record ReasonSelectResult(Guid? Id, string Text);

// 理由の選択 (返品理由・在庫調整理由)。定義済みのタップ、または任意入力
public sealed partial class ReasonSelectViewModel : AppDialogViewModelBase, IPopupInitialize<ReasonSelectParameter>
{
    private readonly IPopupNavigator popupNavigator;

    [ObservableProperty]
    public partial string Title { get; set; } = string.Empty;

    [ObservableProperty]
    public partial IReadOnlyList<ReasonItem> Items { get; set; } = [];

    [ObservableProperty]
    public partial bool AllowCustom { get; set; }

    public EntryController Custom { get; } = new();

    public IObserveCommand SelectCommand { get; }

    public IObserveCommand CloseCommand { get; }

    public IObserveCommand CommitCommand { get; }

    public ReasonSelectViewModel(IPopupNavigator popupNavigator)
    {
        this.popupNavigator = popupNavigator;

        SelectCommand = MakeAsyncCommand<ReasonItem>(async x => await popupNavigator.CloseAsync(new ReasonSelectResult(x.Id, x.Name)));
        CloseCommand = MakeAsyncCommand(async () => await popupNavigator.CloseAsync());
        CommitCommand = MakeAsyncCommand(CommitAsync);
    }

    public void Initialize(ReasonSelectParameter parameter)
    {
        Title = parameter.Title;
        Items = parameter.Items;
        AllowCustom = parameter.AllowCustom;
    }

    private async Task CommitAsync()
    {
        if (String.IsNullOrWhiteSpace(Custom.Text))
        {
            Custom.Focus();
            return;
        }

        await popupNavigator.CloseAsync(new ReasonSelectResult(null, Custom.Text.Trim()));
    }
}
