namespace Pos.Terminal.Modules.Inventory;

// 受領待ちの一覧の行 (種類の文言と色は画面側の Converter で付ける)
public sealed record ReceivingItem(
    ReceivingDocument Document,
    ReceivingKind Kind,
    string Source,
    string Number,
    string Summary,
    string DateText);

// 入荷・移動の受領 (自店、オンライン限定): 入荷予定と自店宛に出荷済みの移動を並べる。タップで検品
public sealed partial class ReceivingListViewModel : AppViewModelBase
{
    private const string EmptyMessage = "受領待ちの入荷・移動はありません。";

    private readonly ReceivingUsecase receiving;

    // 検品・スキャンの画面と共有する状態 (Scope プラグインが注入する)
    [Scope]
    public ReceivingContext ReceivingContext { get; set; } = default!;

    public ObservableCollection<ReceivingItem> Items { get; } = [];

    [ObservableProperty]
    public partial string CountText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Message { get; set; } = EmptyMessage;

    // 取得中 / 取得できない / 結果 (空文字) を切り替える。表示した直後に読むので取得中から始める
    [ObservableProperty]
    public partial string CurrentState { get; set; } = ViewHelper.LoadingState;

    public IObserveCommand SelectCommand { get; }

    public ReceivingListViewModel(
        ReceivingUsecase receiving)
    {
        this.receiving = receiving;

        SelectCommand = MakeAsyncCommand<ReceivingItem>(SelectAsync);
    }

    public override async Task OnNavigatedToAsync(INavigationContext context)
    {
        ReceivingContext.Clear();
        await Navigator.PostActionAsync(LoadAsync);
    }

    private async Task LoadAsync()
    {
        CurrentState = ViewHelper.LoadingState;
        var documents = await receiving.QueryPendingAsync();
        if (documents is null)
        {
            Message = "取得できませんでした。\nオンラインで確認してください。";
            CurrentState = ViewHelper.OfflineState;
            return;
        }

        Items.Replace(documents.Select(static x => new ReceivingItem(
            x,
            x.Kind,
            x.Source,
            x.Number ?? string.Empty,
            ViewHelper.LineSummary(x.Lines.Count == 0 ? null : x.Lines[0].ProductName, x.Lines.Count),
            x.ExpectedDate is { } expected ? $"予定 {ViewHelper.Date(expected)}" : x.ShippedAt is { } shipped ? $"出荷 {ViewHelper.DateTime(shipped)}" : string.Empty)));
        CountText = $"{documents.Count} 件";
        Message = EmptyMessage;
        CurrentState = string.Empty;
    }

    private Task SelectAsync(ReceivingItem item)
    {
        ReceivingContext.Start(item.Document);
        return Navigator.ForwardAsync(ViewId.ReceivingCheck);
    }

    protected override Task OnNotifyBackAsync() => Navigator.ForwardAsync(ViewId.Menu);

    protected override Task OnNotifyFunction1() => OnNotifyBackAsync();

    protected override Task OnNotifyFunction4() => LoadAsync();
}
