namespace Pos.Terminal.Modules.Orders;

// 受注一覧の行 (状態・種別の文言と色は画面側の Converter で付ける)
public sealed record OrderItem(
    Guid Id,
    string OrderNo,
    OrderStatus Status,
    OrderType Type,
    string CustomerName,
    string Summary,
    string TotalText,
    string RequestedDateText,
    bool HasRequestedDate);

// 受注一覧 (自店、オンライン限定): 状態で絞り、受注番号・宛名・電話で検索する。タップで詳細
public sealed partial class OrderListViewModel : AppViewModelBase
{
    private static readonly (string Label, OrderStatus? Status, bool Open)[] Filters =
    [
        ("未完了", null, true),
        ("入荷待ち", OrderStatus.Ordered, false),
        ("引き渡し待ち", OrderStatus.Arrived, false),
        ("完了", OrderStatus.Completed, false),
        ("キャンセル", OrderStatus.Cancelled, false),
        ("すべて", null, false)
    ];

    private readonly IPopupNavigator popupNavigator;

    private readonly Session session;

    private readonly NetworkService network;

    private int filterIndex;

    public EntryController Keyword { get; }

    public ObservableCollection<OrderItem> Items { get; } = [];

    [ObservableProperty]
    public partial string FilterText { get; set; } = Filters[0].Label;

    [ObservableProperty]
    public partial string CountText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Message { get; set; } = "受注がありません。";

    // 取得中 / 取得できない / 結果 (空文字) を切り替える。表示した直後に読むので取得中から始める
    [ObservableProperty]
    public partial string CurrentState { get; set; } = ViewHelper.LoadingState;

    public IObserveCommand SearchCommand { get; }

    public IObserveCommand FilterCommand { get; }

    public IObserveCommand SelectCommand { get; }

    public OrderListViewModel(
        IPopupNavigator popupNavigator,
        Session session,
        NetworkService network)
    {
        this.popupNavigator = popupNavigator;
        this.session = session;
        this.network = network;

        SearchCommand = MakeAsyncCommand(LoadAsync);
        Keyword = new EntryController(SearchCommand);
        FilterCommand = MakeAsyncCommand(SelectFilterAsync);
        SelectCommand = MakeAsyncCommand<OrderItem>(x => Navigator.ForwardAsync(ViewId.OrderDetail, Parameters.Make().WithOrderId(x.Id)));
    }

    public override async Task OnNavigatedToAsync(INavigationContext context)
    {
        await Navigator.PostActionAsync(LoadAsync);
    }

    private async Task LoadAsync()
    {
        if (session.StoreId is not { } storeId)
        {
            CurrentState = string.Empty;
            return;
        }

        CurrentState = ViewHelper.LoadingState;
        var filter = Filters[filterIndex];
        var result = await network.ExecuteAsync(h => h.GetOrdersAsync(storeId, filter.Status, filter.Open, Keyword.Text?.Trim()), notify: false);
        if (!result.IsSuccess)
        {
            Message = "取得できませんでした。\nオンラインで確認してください。";
            CurrentState = ViewHelper.OfflineState;
            return;
        }

        var page = result.Content!;
        Items.Replace(page.Items.Select(static x => new OrderItem(
            x.Id,
            x.OrderNo,
            x.Status,
            x.Type,
            x.CustomerName,
            ViewHelper.LineSummary(x.Lines.Count == 0 ? null : x.Lines[0].ProductName, x.Lines.Count),
            ViewHelper.Yen(x.Total),
            x.RequestedDate is null ? string.Empty : $"希望 {ViewHelper.Date(x.RequestedDate.Value)}",
            x.RequestedDate is not null)));
        CountText = $"{page.Total} 件";
        Message = "受注がありません。";
        CurrentState = string.Empty;
    }

    private async Task SelectFilterAsync()
    {
        var index = await popupNavigator.ChooseAsync(Filters.Select(static x => x.Label).ToArray(), "状態", filterIndex);
        if ((index < 0) || (index == filterIndex))
        {
            return;
        }

        filterIndex = index;
        FilterText = Filters[index].Label;
        await LoadAsync();
    }

    protected override Task OnNotifyBackAsync() => Navigator.ForwardAsync(ViewId.Menu);

    protected override Task OnNotifyFunction1() => OnNotifyBackAsync();

    protected override Task OnNotifyFunction2() => SelectFilterAsync();

    protected override Task OnNotifyFunction4() => LoadAsync();
}
