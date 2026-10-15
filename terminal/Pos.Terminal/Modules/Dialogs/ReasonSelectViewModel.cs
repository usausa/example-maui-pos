namespace Pos.Terminal.Modules.Dialogs;

public sealed record ReasonItem(Guid? Id, string Name);

public sealed record ReasonSelectParameter(string Title, IReadOnlyList<ReasonItem> Items, bool AllowCustom);

public sealed record ReasonSelectResult(Guid? Id, string Text);

// 理由の選択: 定型の理由のタップか任意の入力で理由を返す (取消、返品、値引、入出金、在庫の調整、受注のキャンセル)
public sealed partial class ReasonSelectViewModel : AppDialogViewModelBase, IPopupInitialize<ReasonSelectParameter>
{
    [ObservableProperty]
    public partial string Title { get; set; } = string.Empty;

    public ObservableCollection<ReasonItem> Items { get; } = [];

    [ObservableProperty]
    public partial bool AllowCustom { get; set; }

    public EntryController Custom { get; }

    public IObserveCommand SelectCommand { get; }

    public IObserveCommand CloseCommand { get; }

    public IObserveCommand CommitCommand { get; }

    public ReasonSelectViewModel()
    {
        SelectCommand = MakeAsyncCommand<ReasonItem>(async x => await CloseAsync(new ReasonSelectResult(x.Id, x.Name)));
        CloseCommand = MakeAsyncCommand(async () => await CloseAsync());
        CommitCommand = MakeAsyncCommand(CommitAsync);
        // キーボードの Enter でも確定する (キーボードが出ている間はシートの下段ボタンが隠れる)
        Custom = new EntryController(CommitCommand);
    }

    public void Initialize(ReasonSelectParameter parameter)
    {
        Title = parameter.Title;
        Items.Replace(parameter.Items);
        AllowCustom = parameter.AllowCustom;
    }

    private async Task CommitAsync()
    {
        if (String.IsNullOrWhiteSpace(Custom.Text))
        {
            Custom.Focus();
            return;
        }

        await CloseAsync(new ReasonSelectResult(null, Custom.Text.Trim()));
    }
}
