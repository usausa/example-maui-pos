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

    private readonly OrderUsecase orders;

    private int typeIndex;

    // 販売の画面間で共有する状態 (Scope プラグインが同じインスタンスを注入し、どの画面からも参照されなくなると破棄する)
    [Scope]
    public SalesContext SalesContext { get; set; } = default!;

    [ObservableProperty]
    public partial string TypeText { get; set; } = Types[0].Label;

    public EntryController CustomerName { get; } = new();

    [ObservableProperty]
    public partial string? PhoneText { get; set; }

    [ObservableProperty]
    public partial bool HasRequestedDate { get; set; }

    [ObservableProperty]
    public partial DateTime RequestedDate { get; set; } = DateTime.Today;

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
        OrderUsecase orders)
    {
        this.dialog = dialog;
        this.orders = orders;

        SelectTypeCommand = MakeAsyncCommand(async () =>
        {
            var index = await popupNavigator.ChooseAsync(Types.Select(static x => x.Label).ToArray(), "種別", typeIndex);
            if (index >= 0)
            {
                typeIndex = index;
                TypeText = Types[index].Label;
            }
        });
        InputPhoneCommand = MakeAsyncCommand(async () => PhoneText = await popupNavigator.InputPhoneAsync(PhoneText) ?? PhoneText);
        SetDateCommand = MakeDelegateCommand(() =>
        {
            RequestedDate = DateTime.Today.AddDays(7);
            HasRequestedDate = true;
        });
        ClearDateCommand = MakeDelegateCommand(() => HasRequestedDate = false);
    }

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
            await dialog.InformationAsync("宛名を入力してください。");
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
        await dialog.InformationAsync($"受注 {result.Content.OrderNo} を登録しました ({ViewHelper.Name(result.Content.Status)})。");
        await ReturnAsync();
    }
}
