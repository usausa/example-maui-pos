namespace Pos.Terminal.Modules.Sales;

using Pos.Terminal.Models.Cart;

public sealed record CartLineItem(CartLine Line, string Name, string Detail, string AmountText, string Note, bool HasNote);

// 販売: 明細を組み立てる。計算は SalesUsecase (Pos.Domain の SalesLogic、サーバと同じ)
public sealed partial class SalesViewModel : AppViewModelBase
{
    private enum MoreAction
    {
        Discount,
        ClearDiscount,
        Delivery,
        Hold,
        Recall,
        Clear
    }

    private readonly IDialog dialog;

    private readonly IPopupNavigator popupNavigator;

    private SalesContext salesContext = new();

    private readonly DataAccessor accessor;

    private readonly SalesUsecase sales;

    [ObservableProperty]
    public partial string CustomerText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool HasCustomer { get; set; }

    public ObservableCollection<CartLineItem> Lines { get; } = [];

    [ObservableProperty]
    public partial bool HasLines { get; set; }

    [ObservableProperty]
    public partial string SubtotalText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string DiscountText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string TotalText { get; set; } = string.Empty;

    public IObserveCommand CustomerCommand { get; }

    public IObserveCommand ClearCustomerCommand { get; }

    public IObserveCommand EditLineCommand { get; }

    public IObserveCommand DeleteLineCommand { get; }

    public IObserveCommand MoreCommand { get; }

    public SalesViewModel(
        IDialog dialog,
        IPopupNavigator popupNavigator,
        DataAccessor accessor,
        SalesUsecase sales)
    {
        this.dialog = dialog;
        this.popupNavigator = popupNavigator;
        this.accessor = accessor;
        this.sales = sales;

        CustomerCommand = MakeAsyncCommand(() => Navigator.ForwardAsync(ViewId.CustomerSelect, Parameters.Make().WithReturnTo(ViewId.Sales).WithContext(salesContext)));
        ClearCustomerCommand = MakeDelegateCommand(() =>
        {
            salesContext.Cart.Customer = null;
            Refresh();
        });
        EditLineCommand = MakeAsyncCommand<CartLineItem>(EditLineAsync);
        DeleteLineCommand = MakeDelegateCommand<CartLineItem>(x =>
        {
            salesContext.Cart.Lines.Remove(x.Line);
            Refresh();
        });
        MoreCommand = MakeAsyncCommand(MoreAsync);
    }

    public override Task OnNavigatedToAsync(INavigationContext context)
    {
        salesContext = context.Parameter.GetContext<SalesContext>() ?? new SalesContext();
        Refresh();
        return Task.CompletedTask;
    }

    // 明細の表示と合計を計算し直す
    private void Refresh()
    {
        var cart = salesContext.Cart;
        HasCustomer = cart.Customer is not null;
        CustomerText = cart.Customer is null ? "👤 会員を選択" : $"👤 {cart.Customer.Name}  {DisplayText.Points(cart.Customer.PointBalance)}";

        var result = sales.Calculate(cart, []);
        var items = new List<CartLineItem>(cart.Lines.Count);
        for (var i = 0; i < cart.Lines.Count; i++)
        {
            var line = cart.Lines[i];
            var calculated = result.Lines[i];
            var discount = calculated.DiscountAmount + calculated.AllocatedDiscountAmount;
            var detail = $"{DisplayText.Yen(line.UnitPrice)} × {DisplayText.Quantity(line.Quantity)}";
            if (discount != 0m)
            {
                detail += $"  -{DisplayText.Yen(discount)}";
            }

            var note = String.Join("  ", new[] { line.SerialNumbers.Count > 0 ? "S/N " + String.Join(",", line.SerialNumbers) : null, line.Note }.Where(static x => !String.IsNullOrEmpty(x)));
            items.Add(new CartLineItem(line, line.Product.Name, detail, DisplayText.Yen(calculated.NetAmount), note, note.Length > 0));
        }

        Lines.Replace(items);
        HasLines = items.Count > 0;
        SubtotalText = $"小計 {DisplayText.Yen(result.Subtotal)}";
        DiscountText = result.DiscountTotal == 0m ? string.Empty : $"値引 -{DisplayText.Yen(result.DiscountTotal)}";
        TotalText = $"合計 {DisplayText.Yen(result.Total)}  (内消費税 {DisplayText.Yen(result.TaxTotal)})";
    }

    private async Task EditLineAsync(CartLineItem item)
    {
        var discounts = (await accessor.QueryDiscountListAsync()).Where(static x => x.Scope == DiscountScope.Line).ToList();
        var result = await popupNavigator.PopupAsync<LineEditParameter, LineEditResult>(DialogId.LineEdit, new LineEditParameter(item.Line, discounts));
        if (result == LineEditResult.Delete)
        {
            salesContext.Cart.Lines.Remove(item.Line);
        }

        Refresh();
    }

    private async Task MoreAsync()
    {
        var cart = salesContext.Cart;
        var actions = new List<(string Label, MoreAction Action)>
        {
            ("🏷 取引値引", MoreAction.Discount),
            ("🚚 配送先", MoreAction.Delivery),
            ("⏸ 保留する", MoreAction.Hold),
            ("▶ 保留を呼び出す", MoreAction.Recall),
            ("🧹 クリア", MoreAction.Clear)
        };
        if (cart.Discounts.Count > 0)
        {
            actions.Insert(1, ("取引値引を解除", MoreAction.ClearDiscount));
        }

        var index = await dialog.ChooseAsync(actions.Select(static x => x.Label).ToArray(), "操作");
        if (index < 0)
        {
            return;
        }

        switch (actions[index].Action)
        {
            case MoreAction.Discount:
                var discounts = (await accessor.QueryDiscountListAsync()).Where(static x => x.Scope == DiscountScope.Transaction).ToList();
                var result = sales.Calculate(cart, []);
                var discount = await popupNavigator.PopupAsync<DiscountParameter, CartDiscount?>(DialogId.Discount, new DiscountParameter("取引値引", discounts, result.NetSubtotal));
                if (discount is not null)
                {
                    cart.Discounts.Add(discount);
                    Refresh();
                }

                break;

            case MoreAction.ClearDiscount:
                cart.Discounts.Clear();
                Refresh();
                break;

            case MoreAction.Delivery:
                await Navigator.ForwardAsync(ViewId.Delivery, Parameters.Make().WithContext(salesContext));
                break;

            case MoreAction.Hold:
                if (cart.IsEmpty)
                {
                    await dialog.InformationAsync("明細がありません。");
                    return;
                }

                await sales.HoldAsync(cart);
                salesContext.Reset();
                Refresh();
                await dialog.Toast("保留しました。");
                break;

            case MoreAction.Recall:
                await Navigator.ForwardAsync(ViewId.Hold, Parameters.Make().WithContext(salesContext));
                break;

            case MoreAction.Clear:
                if (cart.IsEmpty || await dialog.AskAsync("明細をすべて削除しますか？", null, "削除"))
                {
                    salesContext.Reset();
                    Refresh();
                }

                break;
        }
    }

    protected override Task OnNotifyBackAsync() => Navigator.ForwardAsync(ViewId.Menu);

    protected override Task OnNotifyFunction1() => OnNotifyBackAsync();

    protected override Task OnNotifyFunction2() =>
        Navigator.ForwardAsync(ViewId.Scan, Parameters.Make().WithScan(ScanMode.Product, ViewId.Sales).WithContext(salesContext));

    protected override Task OnNotifyFunction3() =>
        Navigator.ForwardAsync(ViewId.ProductSearch, Parameters.Make().WithReturnTo(ViewId.Sales).WithContext(salesContext));

    protected override async Task OnNotifyFunction4()
    {
        if (salesContext.Cart.IsEmpty)
        {
            await dialog.InformationAsync("明細がありません。");
            return;
        }

        // シリアル番号が必要な商品の確認
        var missing = salesContext.Cart.Lines.FirstOrDefault(static x => x.Product.RequiresSerial && (x.SerialNumbers.Count == 0));
        if (missing is not null)
        {
            await dialog.InformationAsync($"{missing.Product.Name} のシリアル番号を入力してください。");
            return;
        }

        await Navigator.ForwardAsync(ViewId.Payment, Parameters.Make().WithContext(salesContext));
    }
}
