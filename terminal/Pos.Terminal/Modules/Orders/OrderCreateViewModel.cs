namespace Pos.Terminal.Modules.Orders;

using Pos.Domain.Logic;
using Pos.Terminal.Modules.Sales;

// 受注にする (オンライン限定): 販売のカートを取り寄せ / 取り置きとして登録し、カートを空にする。会員がいなければ宛名が必要
public sealed partial class OrderCreateViewModel : AppViewModelBase
{
    private static readonly (string Label, OrderType Type)[] Types =
    [
        ("🚚 取り寄せ (入荷を待つ)", OrderType.BackOrder),
        ("🔖 取り置き (店頭の在庫を確保)", OrderType.Hold)
    ];

    private readonly IDialog dialog;

    private readonly Session session;

    private readonly OrderUsecase orders;

    private int typeIndex;

    // 販売の画面間で共有する状態 (Scope プラグインが同じインスタンスを注入し、どの画面からも参照されなくなると破棄する)
    [Scope]
    public SalesContext SalesContext { get; set; } = default!;

    [ObservableProperty]
    public partial string TypeText { get; set; } = ViewHelper.Name(Types[0].Type);

    public EntryController CustomerName { get; } = new();

    [ObservableProperty]
    public partial string? PhoneText { get; set; }

    [ObservableProperty]
    public partial bool HasRequestedDate { get; set; }

    [ObservableProperty]
    public partial DateTime RequestedDate { get; set; }

    public EntryController Note { get; } = new();

    [ObservableProperty]
    public partial string CartText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string CustomerText { get; set; } = string.Empty;

    public IObserveCommand SelectTypeCommand { get; }

    public IObserveCommand InputPhoneCommand { get; }

    public IObserveCommand SetDateCommand { get; }

    public IObserveCommand ClearDateCommand { get; }

    public OrderCreateViewModel(
        IDialog dialog,
        IPopupNavigator popupNavigator,
        Session session,
        OrderUsecase orders)
    {
        this.dialog = dialog;
        this.session = session;
        this.orders = orders;

        RequestedDate = BusinessDay;

        SelectTypeCommand = MakeAsyncCommand(async () =>
        {
            var index = await popupNavigator.ChooseAsync(Types.Select(static x => x.Label).ToArray(), "種別", typeIndex);
            if (index >= 0)
            {
                typeIndex = index;
                TypeText = ViewHelper.Name(Types[index].Type);
            }
        });
        InputPhoneCommand = MakeAsyncCommand(async () => PhoneText = await popupNavigator.InputPhoneAsync(PhoneText) ?? PhoneText);
        SetDateCommand = MakeDelegateCommand(() =>
        {
            RequestedDate = BusinessDay.AddDays(7);
            HasRequestedDate = true;
        });
        ClearDateCommand = MakeDelegateCommand(() => HasRequestedDate = false);
    }

    // 希望日の基準は営業日 (切替時刻より前は前日)
    private DateTime BusinessDay => session.BusinessDate.ToDateTime(TimeOnly.MinValue);

    // 会員の名前と電話を初期値にする
    public override Task OnNavigatedToAsync(INavigationContext context)
    {
        var cart = SalesContext.Cart;
        var customer = cart.Customer;
        CustomerName.Text = customer?.Name;
        PhoneText = customer?.Phone;
        CustomerText = customer is null ? "会員なし (宛名が必要です)" : $"👤 {customer.Name}  {customer.Code}";
        CartText = $"{cart.Summary}  {ViewHelper.Yen(cart.Lines.Sum(static x => OrderLogic.LineAmount(x.UnitPrice, x.Quantity)))}";
        return Task.CompletedTask;
    }

    private Task<bool> ReturnAsync() => Navigator.ForwardAsync(ViewId.Sales);

    protected override Task OnNotifyBackAsync() => ReturnAsync();

    protected override Task OnNotifyFunction1() => OnNotifyBackAsync();

    protected override async Task OnNotifyFunction4()
    {
        var cart = SalesContext.Cart;
        if ((cart.Customer is null) && String.IsNullOrWhiteSpace(CustomerName.Text))
        {
            await dialog.InformationAsync("宛名を入力してください");
            CustomerName.Focus();
            return;
        }

        var type = Types[typeIndex].Type;
        var result = await orders.CreateAsync(
            cart,
            type,
            CustomerName.Text.TrimToNull(),
            PhoneText.TrimToNull(),
            HasRequestedDate ? DateOnly.FromDateTime(RequestedDate) : null,
            Note.Text.TrimToNull());
        if (result is not { IsSuccess: true, Content: not null })
        {
            return;
        }

        SalesContext.Reset();
        await dialog.InformationAsync($"受注 {result.Content.OrderNo} を登録しました ({ViewHelper.Name(result.Content.Status)})");
        await ReturnAsync();
    }
}
