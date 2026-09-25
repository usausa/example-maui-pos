namespace Pos.Terminal.Modules.Orders;

using Pos.Contract.Orders;
using Pos.Domain.Logic;
using Pos.Terminal.Modules.Dialogs;
using Pos.Terminal.Modules.Sales;

// 受注詳細 (オンライン限定): 連絡先・明細・前受金・経過。入荷待ちは [入荷]、未完了は [操作] (前受金の受取・返金、キャンセル)、
// 引き渡し待ちは [会計へ] (明細と前受金をカートに入れて販売へ)
public sealed partial class OrderDetailViewModel : AppViewModelBase
{
    private enum OrderAction
    {
        Deposit,
        RefundDeposit,
        Cancel
    }

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

    private Dictionary<Guid, PaymentMethodResponseItem> paymentMethods = [];

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

    [ObservableProperty]
    public partial string DepositText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool HasDeposit { get; set; }

    public ObservableCollection<SummarySection> Sections { get; } = [];

    [ObservableProperty]
    public partial bool CanArrive { get; set; }

    [ObservableProperty]
    public partial bool CanOperate { get; set; }

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
        paymentMethods = await orders.QueryPaymentMethodsAsync();
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
        HasDeposit = value.DepositAmount > 0;
        DepositText = $"前受金 {ViewHelper.Yen(value.DepositAmount)}";

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

        var sections = new List<SummarySection>
        {
            new("👤 お客様",
            [
                new SummaryRow("電話", value.Phone ?? "-"),
                new SummaryRow("希望日", value.RequestedDate is null ? "指定なし" : ViewHelper.Date(value.RequestedDate.Value)),
                new SummaryRow("備考", value.Note ?? "-")
            ]),
            new("📦 明細", value.Lines
                .Select(static x => new SummaryRow($"{x.ProductName}\n{ViewHelper.Yen(x.UnitPrice)} × {ViewHelper.Quantity(x.Quantity)}", ViewHelper.Yen(x.Amount)))
                .ToList())
        };
        if (value.Deposits.Count > 0)
        {
            var rows = value.Deposits
                .Select(x => new SummaryRow(
                    $"{ViewHelper.Name(x.Type)} {MethodName(x.PaymentMethodId, x.Kind)}\n{ViewHelper.DateTime(x.OccurredAt)}",
                    x.Type == OrderDepositType.Refund ? ViewHelper.MinusYen(x.Amount) : ViewHelper.Yen(x.Amount)))
                .ToList();
            if (value.Status.IsOpen())
            {
                rows.Add(new SummaryRow("会計で充てる額", ViewHelper.Yen(value.DepositAmount)));
            }

            sections.Add(new SummarySection("💴 前受金", rows));
        }

        sections.Add(new SummarySection("🕒 経過", progress));
        Sections.Replace(sections);

        CanArrive = value.Status == OrderStatus.Ordered;
        CanOperate = value.Status.IsOpen();
        CanCheckout = value.Status == OrderStatus.Arrived;
    }

    private string MethodName(Guid id, PaymentKind kind) =>
        paymentMethods.TryGetValue(id, out var method) ? method.Name : ViewHelper.Name(kind);

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

    // 操作: 前受金の受取 (前受金がないとき) か返金 (あるとき)、キャンセル
    protected override async Task OnNotifyFunction3()
    {
        if ((order is null) || !CanOperate)
        {
            return;
        }

        var actions = new List<(string Label, OrderAction Action)>
        {
            order.DepositAmount > 0 ? ("↩️ 前受金を返す", OrderAction.RefundDeposit) : ("💴 前受金を受け取る", OrderAction.Deposit),
            ("✖️ 受注をキャンセル", OrderAction.Cancel)
        };
        var index = await popupNavigator.ChooseAsync(actions.Select(static x => x.Label).ToArray(), "操作");
        if (index < 0)
        {
            return;
        }

        switch (actions[index].Action)
        {
            case OrderAction.Deposit:
                await DepositAsync(order);
                break;

            case OrderAction.RefundDeposit:
                if (await RefundDepositAsync(order, false))
                {
                    await dialog.Toast("前受金を返しました。");
                }

                break;

            case OrderAction.Cancel:
                await CancelAsync(order);
                break;
        }
    }

    // 前受金の受取: 支払方法と金額 (受注の金額まで) を選んで登録する。現金はシフトの予想現金に入る
    private async Task DepositAsync(OrderResponseItem target)
    {
        if (!session.CanTransact)
        {
            await dialog.InformationAsync("レジを開設してから前受金を受け取ってください。");
            return;
        }

        var methods = paymentMethods.Values
            .Where(static x => x.IsActive && !x.IsDeleted && x.Kind.CanReceiveDeposit())
            .OrderBy(static x => x.SortOrder)
            .ToList();
        var method = await popupNavigator.ChooseAsync(methods, static x => x.Name, "前受金の支払方法");
        if (method is null)
        {
            return;
        }

        var text = await popupNavigator.InputAmountAsync($"前受金 (受注の金額 {ViewHelper.Yen(target.Total)} まで)", 0m);
        if ((text is null) || !Decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out var amount))
        {
            return;
        }

        if (OrderLogic.ValidateDeposit(target.Status, target.DepositAmount, target.Total, amount, method.Kind) is { } error)
        {
            await dialog.InformationAsync(ViewHelper.Reason(error.Reason));
            return;
        }

        string? reference = null;
        if (method.RequiresReference)
        {
            reference = (await popupNavigator.InputReferenceAsync(method.Name))?.Trim();
            if (String.IsNullOrEmpty(reference))
            {
                return;
            }
        }

        if (!await dialog.AskAsync($"前受金 {ViewHelper.Yen(amount)} を{method.Name}で受け取りますか？", "前受金", "受け取る"))
        {
            return;
        }

        var result = await orders.DepositAsync(target, method, amount, reference);
        if (result is { IsSuccess: true, Content: not null })
        {
            Update(result.Content);
            await dialog.Toast("前受金を受け取りました。");
        }
    }

    // 前受金の返金 (全額を受け取った方法で)。キャンセルの前に返すときは、キャンセルまでを確かめる。返したら true
    private async Task<bool> RefundDepositAsync(OrderResponseItem target, bool beforeCancel)
    {
        if (!session.CanTransact)
        {
            await dialog.InformationAsync("レジを開設してから前受金を返してください。");
            return false;
        }

        var refund = $"前受金 {ViewHelper.Yen(target.DepositAmount)} を{DepositMethodName(target)}で返";
        var confirmed = beforeCancel
            ? await dialog.AskAsync($"{refund}して、{target.OrderNo} をキャンセルしますか？", "キャンセル", "返金してキャンセル")
            : await dialog.AskAsync($"{refund}しますか？", "前受金の返金", "返金");
        if (!confirmed)
        {
            return false;
        }

        var result = await orders.RefundDepositAsync(target);
        if (result is not { IsSuccess: true, Content: not null })
        {
            return false;
        }

        Update(result.Content);
        return true;
    }

    // キャンセル: 前受金があれば返してからキャンセルする
    private async Task CancelAsync(OrderResponseItem target)
    {
        var reason = await popupNavigator.PopupAsync<ReasonSelectParameter, ReasonSelectResult?>(DialogId.ReasonSelect, new ReasonSelectParameter("キャンセルの理由", CancelReasons, true));
        if (reason is null)
        {
            return;
        }

        if (target.DepositAmount > 0)
        {
            if (!await RefundDepositAsync(target, true))
            {
                return;
            }
        }
        else if (!await dialog.AskAsync($"{target.OrderNo} をキャンセルしますか？", "キャンセル", "キャンセル"))
        {
            return;
        }

        var result = await network.ExecuteAsync(h => h.PostOrderCancelAsync(target.Id, new OrderCancelRequest { Reason = reason.Text }));
        if (result is { IsSuccess: true, Content: not null })
        {
            Update(result.Content);
            await dialog.Toast("キャンセルしました。");
        }
    }

    // 前受金を返す方法 (有効な前受金は 1 つなので最後の受取)
    private string DepositMethodName(OrderResponseItem target) =>
        target.Deposits.LastOrDefault(static x => x.Type == OrderDepositType.Receive) is { } received ? MethodName(received.PaymentMethodId, received.Kind) : "-";

    // 会計へ: 受注の明細・会員・前受金をカートに入れて販売へ (会計で受注が完了になる)
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
