namespace Pos.Terminal.Modules.Sales;

using System.Text.Json;

using Pos.Domain.Sales;
using Pos.Terminal.Models.Entity;
using Pos.Terminal.Models.Sales;

public sealed record CartLineItem(CartLine Line, string Name, string Detail, string AmountText, string Note, bool HasNote);

// T-10 販売: 明細を組み立てる。計算は Pos.Domain の SalesCalculator (サーバと同じ)
public sealed partial class SalesViewModel : AppViewModelBase
{
    private readonly IDialog dialog;

    private readonly IPopupNavigator popupNavigator;

    private readonly DataAccessor accessor;

    private readonly Session session;

    private readonly SalesState sales;

    [ObservableProperty]
    public partial string CustomerText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool HasCustomer { get; set; }

    [ObservableProperty]
    public partial IReadOnlyList<CartLineItem> Lines { get; set; } = [];

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
        Session session,
        SalesState sales)
    {
        this.dialog = dialog;
        this.popupNavigator = popupNavigator;
        this.accessor = accessor;
        this.session = session;
        this.sales = sales;

        CustomerCommand = MakeAsyncCommand(() => Navigator.ForwardAsync(ViewId.CustomerSelect, Parameters.Make().WithReturnTo(ViewId.Sales)));
        ClearCustomerCommand = MakeDelegateCommand(() =>
        {
            sales.Cart.Customer = null;
            Refresh();
        });
        EditLineCommand = MakeAsyncCommand<CartLineItem>(EditLineAsync);
        DeleteLineCommand = MakeDelegateCommand<CartLineItem>(x =>
        {
            sales.Cart.Lines.Remove(x.Line);
            Refresh();
        });
        MoreCommand = MakeAsyncCommand(MoreAsync);
    }

    public override Task OnNavigatedToAsync(INavigationContext context)
    {
        Refresh();
        return Task.CompletedTask;
    }

    // 明細の表示と合計を計算し直す
    private void Refresh()
    {
        var cart = sales.Cart;
        HasCustomer = cart.Customer is not null;
        CustomerText = cart.Customer is null ? "👤 会員を選択" : $"👤 {cart.Customer.Name}  {DisplayText.Points(cart.Customer.PointBalance)}";

        var result = Calculate(cart, session);
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

        Lines = items;
        HasLines = items.Count > 0;
        SubtotalText = $"小計 {DisplayText.Yen(result.Subtotal)}";
        DiscountText = result.DiscountTotal == 0m ? string.Empty : $"値引 -{DisplayText.Yen(result.DiscountTotal)}";
        TotalText = $"合計 {DisplayText.Yen(result.Total)}  (内消費税 {DisplayText.Yen(result.TaxTotal)})";
    }

    public static SalesResult Calculate(Cart cart, Session session) =>
        SalesCalculator.Calculate(TransactionBuilder.ToSalesInput(cart, [], session.TaxRounding, session.PointBasis));

    private async Task EditLineAsync(CartLineItem item)
    {
        var discounts = (await accessor.QueryDiscountListAsync()).Where(static x => x.Scope == DiscountScope.Line).ToList();
        var result = await popupNavigator.PopupAsync<LineEditParameter, LineEditResult>(DialogId.LineEdit, new LineEditParameter(item.Line, discounts));
        if (result == LineEditResult.Delete)
        {
            sales.Cart.Lines.Remove(item.Line);
        }

        Refresh();
    }

    private async Task MoreAsync()
    {
        var cart = sales.Cart;
        var hasDiscount = cart.Discounts.Count > 0;
        var items = new List<string> { "🏷 取引値引", "🚚 配送先", "⏸ 保留する", "▶ 保留を呼び出す", "🧹 クリア" };
        if (hasDiscount)
        {
            items.Insert(1, "取引値引を解除");
        }

        var index = await dialog.ChooseAsync(items.ToArray(), "操作");
        if (index < 0)
        {
            return;
        }

        var selected = items[index];
        switch (selected)
        {
            case "🏷 取引値引":
                var discounts = (await accessor.QueryDiscountListAsync()).Where(static x => x.Scope == DiscountScope.Transaction).ToList();
                var result = Calculate(cart, session);
                var discount = await popupNavigator.PopupAsync<DiscountParameter, CartDiscount?>(DialogId.Discount, new DiscountParameter("取引値引", discounts, result.NetSubtotal));
                if (discount is not null)
                {
                    cart.Discounts.Add(discount);
                    Refresh();
                }

                break;

            case "取引値引を解除":
                cart.Discounts.Clear();
                Refresh();
                break;

            case "🚚 配送先":
                await Navigator.ForwardAsync(ViewId.Delivery);
                break;

            case "⏸ 保留する":
                if (cart.IsEmpty)
                {
                    await dialog.InformationAsync("明細がありません。");
                    return;
                }

                await accessor.InsertHoldCartAsync(new HoldCartEntity
                {
                    Id = Guid.NewGuid(),
                    CreatedAt = DateTime.UtcNow,
                    Summary = cart.Summary,
                    Total = Calculate(cart, session).Total,
                    Payload = JsonSerializer.Serialize(cart, HttpService.JsonOptions)
                });
                sales.ResetSale();
                Refresh();
                await dialog.Toast("保留しました。");
                break;

            case "▶ 保留を呼び出す":
                await Navigator.ForwardAsync(ViewId.Hold);
                break;

            case "🧹 クリア":
                if (cart.IsEmpty || await dialog.AskAsync("明細をすべて削除しますか？", null, "削除"))
                {
                    sales.ResetSale();
                    Refresh();
                }

                break;
        }
    }

    protected override Task OnNotifyBackAsync() => Navigator.ForwardAsync(ViewId.Menu);

    protected override Task OnNotifyFunction1() => OnNotifyBackAsync();

    protected override Task OnNotifyFunction2() =>
        Navigator.ForwardAsync(ViewId.Scan, Parameters.Make().WithScan(ScanMode.Product, ViewId.Sales));

    protected override Task OnNotifyFunction3() =>
        Navigator.ForwardAsync(ViewId.ProductSearch, Parameters.Make().WithReturnTo(ViewId.Sales));

    protected override async Task OnNotifyFunction4()
    {
        if (sales.Cart.IsEmpty)
        {
            await dialog.InformationAsync("明細がありません。");
            return;
        }

        // シリアル番号が必要な商品の確認
        var missing = sales.Cart.Lines.FirstOrDefault(static x => x.Product.RequiresSerial && (x.SerialNumbers.Count == 0));
        if (missing is not null)
        {
            await dialog.InformationAsync($"{missing.Product.Name} のシリアル番号を入力してください。");
            return;
        }

        await Navigator.ForwardAsync(ViewId.Payment);
    }
}
