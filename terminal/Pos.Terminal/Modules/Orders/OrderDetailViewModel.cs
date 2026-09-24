namespace Pos.Terminal.Modules.Orders;

using Pos.Contract.Orders;
using Pos.Terminal.Modules.Dialogs;
using Pos.Terminal.Modules.Sales;

// 受注詳細 (オンライン限定): 連絡先・明細・経過。入荷待ちは [入荷]、未完了は [キャンセル]、引き渡し待ちは [会計へ] (明細をカートに入れて販売へ)
public sealed partial class OrderDetailViewModel : AppViewModelBase
{
    private static readonly ReasonItem[] CancelReasons =
    [
        new(null, "お客様都合"),
        new(null, "入荷できない"),
        new(null, "二重登録")
    ];

    private readonly IDialog dialog;

    private readonly IPopupNavigator popupNavigator;

    private readonly Session session;

    private readonly NetworkService network;

    private readonly OrderUsecase orders;

    private Guid orderId;

    private OrderResponseItem? order;

    // 会計へ進むときにカートを渡す (Scope プラグインが販売の画面と同じインスタンスを注入する)
    [Scope]
    public SalesContext SalesContext { get; set; } = default!;

    // 状態・種別の文言と色は画面側の Converter で付ける
    [ObservableProperty]
    public partial OrderStatus Status { get; set; }

    [ObservableProperty]
    public partial OrderType Type { get; set; }

    [ObservableProperty]
    public partial string OrderNo { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string CustomerName { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string TotalText { get; set; } = string.Empty;

    public ObservableCollection<SummarySection> Sections { get; } = [];

    [ObservableProperty]
    public partial bool CanArrive { get; set; }

    [ObservableProperty]
    public partial bool CanCancel { get; set; }

    [ObservableProperty]
    public partial bool CanCheckout { get; set; }

    public OrderDetailViewModel(
        IDialog dialog,
        IPopupNavigator popupNavigator,
        Session session,
        NetworkService network,
        OrderUsecase orders)
    {
        this.dialog = dialog;
        this.popupNavigator = popupNavigator;
        this.session = session;
        this.network = network;
        this.orders = orders;
    }

    public override async Task OnNavigatedToAsync(INavigationContext context)
    {
        var id = context.Parameter.GetOrderId();
        if (id is null)
        {
            await Navigator.PostForwardAsync(ViewId.OrderList);
            return;
        }

        orderId = id.Value;
        await Navigator.PostActionAsync(LoadAsync);
    }

    private async Task LoadAsync()
    {
        var result = await network.ExecuteAsync(h => h.GetOrderAsync(orderId), notifyNotFound: true);
        if (result is { IsSuccess: true, Content: not null })
        {
            Update(result.Content);
        }
    }

    private void Update(OrderResponseItem value)
    {
        order = value;
        Status = value.Status;
        Type = value.Type;
        OrderNo = value.OrderNo;
        CustomerName = value.CustomerName;
        TotalText = ViewHelper.Yen(value.Total);

        var progress = new List<SummaryRow> { new("受注", ViewHelper.DateTime(value.OrderedAt)) };
        if (value.ArrivedAt is not null)
        {
            progress.Add(new SummaryRow("入荷", ViewHelper.DateTime(value.ArrivedAt.Value)));
        }

        if (value.CompletedAt is not null)
        {
            progress.Add(new SummaryRow("完了", ViewHelper.DateTime(value.CompletedAt.Value)));
        }

        if (value.CancelledAt is not null)
        {
            progress.Add(new SummaryRow("キャンセル", ViewHelper.DateTime(value.CancelledAt.Value)));
            progress.Add(new SummaryRow("理由", value.CancelReason ?? "-"));
        }

        Sections.Replace(
        [
            new SummarySection("👤 お客様",
            [
                new SummaryRow("電話", value.Phone ?? "-"),
                new SummaryRow("希望日", value.RequestedDate is null ? "指定なし" : ViewHelper.Date(value.RequestedDate.Value)),
                new SummaryRow("備考", value.Note ?? "-")
            ]),
            new SummarySection("📦 明細", value.Lines
                .Select(static x => new SummaryRow($"{x.ProductName}\n{ViewHelper.Yen(x.UnitPrice)} × {ViewHelper.Quantity(x.Quantity)}", ViewHelper.Yen(x.Amount)))
                .ToList()),
            new SummarySection("🕒 経過", progress)
        ]);

        CanArrive = value.Status == OrderStatus.Ordered;
        CanCancel = value.Status.IsOpen();
        CanCheckout = value.Status == OrderStatus.Arrived;
    }

    protected override Task OnNotifyBackAsync() => Navigator.ForwardAsync(ViewId.OrderList);

    protected override Task OnNotifyFunction1() => OnNotifyBackAsync();

    // 入荷 (取り寄せの商品が届いた)
    protected override async Task OnNotifyFunction2()
    {
        if ((order is null) || !CanArrive || !await dialog.AskAsync($"{order.OrderNo} を入荷にしますか？\nお客様への連絡が必要になります。", "入荷", "入荷"))
        {
            return;
        }

        var result = await network.ExecuteAsync(h => h.PostOrderArriveAsync(order.Id));
        if (result is { IsSuccess: true, Content: not null })
        {
            Update(result.Content);
            await dialog.Toast("引き渡し待ちにしました。");
        }
    }

    protected override async Task OnNotifyFunction3()
    {
        if ((order is null) || !CanCancel)
        {
            return;
        }

        var reason = await popupNavigator.PopupAsync<ReasonSelectParameter, ReasonSelectResult?>(DialogId.ReasonSelect, new ReasonSelectParameter("キャンセルの理由", CancelReasons, true));
        if ((reason is null) || !await dialog.AskAsync($"{order.OrderNo} をキャンセルしますか？", "キャンセル", "キャンセル"))
        {
            return;
        }

        var result = await network.ExecuteAsync(h => h.PostOrderCancelAsync(order.Id, new OrderCancelRequest { Reason = reason.Text }));
        if (result is { IsSuccess: true, Content: not null })
        {
            Update(result.Content);
            await dialog.Toast("キャンセルしました。");
        }
    }

    // 会計へ: 受注の明細と会員をカートに入れて販売へ (会計で受注が完了になる)
    protected override async Task OnNotifyFunction4()
    {
        if ((order is null) || !CanCheckout)
        {
            return;
        }

        if (!session.CanTransact)
        {
            await dialog.InformationAsync("レジを開設してから会計してください。");
            return;
        }

        if (!SalesContext.Cart.IsEmpty && !await dialog.AskAsync("販売中の明細を受注の明細に置き換えますか？", null, "置き換え"))
        {
            return;
        }

        var (cart, missing) = await orders.ToCartAsync(order);
        if (cart is null)
        {
            await dialog.InformationAsync($"{missing} が端末の商品にありません。\n設定・同期でマスタを同期してください。");
            return;
        }

        SalesContext.Reset();
        SalesContext.Cart = cart;
        await Navigator.ForwardAsync(ViewId.Sales);
    }
}
