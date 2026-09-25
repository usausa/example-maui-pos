namespace Pos.Terminal.Modules.Sales;

using Pos.Domain.Logic;
using Pos.Terminal.Models.Cart;

public sealed record MethodItem(PaymentMethodResponseItem Method, string Name);

// Removable = false は受注の前受金 (全額を充てるので外せない)
public sealed record PaymentItem(CartPayment Payment, string Text, string AmountText, bool Removable);

// 会計: 埋め込みテンキーで預り金を入れ、支払方法ボタンで支払を積む。確定は SalesUsecase で登録して会計完了へ。
// 受注の前受金は先頭の支払にする (合計を超えるときは会計できないので、受注の詳細で返してもらう)
public sealed partial class PaymentViewModel : AppViewModelBase
{
    private readonly IDialog dialog;

    private readonly IPopupNavigator popupNavigator;

    private readonly Session session;

    private readonly DataAccessor accessor;

    private readonly SalesUsecase sales;

    private PaymentMethodResponseItem? pointsMethod;

    private PaymentMethodResponseItem? depositMethod;

    private SalesResult result = default!;

    private decimal remaining;

    // 販売の画面間で共有する状態 (Scope プラグインが同じインスタンスを注入し、どの画面からも参照されなくなると破棄する)
    [Scope]
    public SalesContext SalesContext { get; set; } = default!;

    public NumberInputModel Input { get; } = new() { MaxLength = 8 };

    [ObservableProperty]
    public partial string Title { get; set; } = "会計";

    [ObservableProperty]
    public partial string TotalText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string PointsText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool PointsEnabled { get; set; }

    [ObservableProperty]
    public partial bool CustomerEnabled { get; set; } = true;

    [ObservableProperty]
    public partial string PaidText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string RemainingCaption { get; set; } = "残り";

    [ObservableProperty]
    public partial string RemainingText { get; set; } = string.Empty;

    public ObservableCollection<PaymentItem> Payments { get; } = [];

    public ObservableCollection<MethodItem> Methods { get; } = [];

    [ObservableProperty]
    public partial string InputText { get; set; } = ViewHelper.Yen(0);

    [ObservableProperty]
    public partial bool CanConfirm { get; set; }

    [ObservableProperty]
    public partial bool HasPayments { get; set; }

    public IObserveCommand PushCommand { get; }

    public IObserveCommand PopCommand { get; }

    public IObserveCommand AddAmountCommand { get; }

    public IObserveCommand ExactCommand { get; }

    public IObserveCommand AddPaymentCommand { get; }

    public IObserveCommand RemovePaymentCommand { get; }

    public PaymentViewModel(
        IDialog dialog,
        IPopupNavigator popupNavigator,
        Session session,
        DataAccessor accessor,
        SalesUsecase sales)
    {
        this.dialog = dialog;
        this.popupNavigator = popupNavigator;
        this.session = session;
        this.accessor = accessor;
        this.sales = sales;

        PushCommand = MakeDelegateCommand<string>(x =>
        {
            Input.Push(x);
            InputText = ViewHelper.Yen(InputValue);
        });
        PopCommand = MakeDelegateCommand(() =>
        {
            Input.Pop();
            InputText = ViewHelper.Yen(InputValue);
        });
        AddAmountCommand = MakeDelegateCommand<string>(x => SetInput(InputValue + Int32.Parse(x, CultureInfo.InvariantCulture)));
        ExactCommand = MakeDelegateCommand(() => SetInput(Math.Max(0, remaining)));
        AddPaymentCommand = MakeAsyncCommand<MethodItem>(AddPaymentAsync);
        RemovePaymentCommand = MakeDelegateCommand<PaymentItem>(x =>
        {
            if (x.Removable)
            {
                SalesContext.Payments.Remove(x.Payment);
                Refresh();
            }
        });
    }

    private decimal InputValue => Decimal.TryParse(Input.NormalizeText, NumberStyles.Number, CultureInfo.InvariantCulture, out var value) ? value : 0m;

    public override async Task OnNavigatedToAsync(INavigationContext context)
    {
        // 会員が変わったらポイント支払は無効
        if (SalesContext.Cart.Customer is null)
        {
            for (var i = SalesContext.Payments.Count - 1; i >= 0; i--)
            {
                if (SalesContext.Payments[i].Method.Kind == PaymentKind.Points)
                {
                    SalesContext.Payments.RemoveAt(i);
                }
            }
        }

        Refresh();
        await Navigator.PostActionAsync(LoadMethodsAsync);
    }

    private async Task LoadMethodsAsync()
    {
        var methods = (await accessor.QueryPaymentMethodListAsync()).Where(static x => x.IsActive && !x.IsDeleted).OrderBy(static x => x.SortOrder).ToList();
        pointsMethod = methods.FirstOrDefault(static x => x.Kind == PaymentKind.Points);
        depositMethod = methods.FirstOrDefault(static x => x.Kind == PaymentKind.Deposit);
        Methods.Replace(methods.Where(static x => x.Kind is not (PaymentKind.Points or PaymentKind.Deposit)).Select(static x => new MethodItem(x, String.IsNullOrEmpty(x.ShortName) ? x.Name : x.ShortName)));
        if (!await ApplyDepositAsync())
        {
            await Navigator.ForwardAsync(ViewId.Sales);
            return;
        }

        Refresh();
    }

    // 受注の前受金を先頭の支払にする (入り直すたびに置き直す)。会計できないときは false
    private async ValueTask<bool> ApplyDepositAsync()
    {
        var payments = SalesContext.Payments;
        for (var i = payments.Count - 1; i >= 0; i--)
        {
            if (payments[i].Method.Kind == PaymentKind.Deposit)
            {
                payments.RemoveAt(i);
            }
        }

        var deposit = SalesContext.Cart.DepositAmount;
        if (deposit <= 0)
        {
            return true;
        }

        if (depositMethod is null)
        {
            await dialog.InformationAsync("前受金の支払方法がありません。\n設定・同期でマスタを同期してください。");
            return false;
        }

        var total = sales.Calculate(SalesContext.Cart, []).Total;
        if (deposit > total)
        {
            await dialog.InformationAsync($"前受金 {ViewHelper.Yen(deposit)} が合計 {ViewHelper.Yen(total)} を超えています。\n受注の詳細で前受金を返してから会計してください。");
            return false;
        }

        payments.Insert(0, new CartPayment { Id = Guid.NewGuid(), Method = depositMethod, Amount = deposit, TenderedAmount = deposit });
        return true;
    }

    private void SetInput(decimal value)
    {
        Input.Text = value.ToString("0", CultureInfo.InvariantCulture);
        InputText = ViewHelper.Yen(value);
    }

    private void Refresh()
    {
        var cart = SalesContext.Cart;
        var payments = SalesContext.Payments;
        result = sales.Calculate(cart, payments);

        var paid = payments.Sum(static x => x.Amount);
        remaining = result.Total - paid;

        TotalText = ViewHelper.Yen(result.Total);
        var customer = cart.Customer;
        var pointsUsed = payments.Where(static x => x.Method.Kind == PaymentKind.Points).Sum(static x => x.Amount);
        PointsText = customer is null ? "会員なし" : $"{ViewHelper.Yen(pointsUsed)} / 残高 {ViewHelper.Points(customer.PointBalance)}";
        PointsEnabled = (customer is not null) && (pointsMethod is not null) && (customer.PointBalance > 0);
        PaidText = ViewHelper.Yen(paid);
        Payments.Replace(payments.Select(static x => new PaymentItem(x, x.Reference is null ? x.Method.Name : $"{x.Method.Name} {x.Reference}", ViewHelper.Yen(x.Amount), x.Method.Kind != PaymentKind.Deposit)));
        HasPayments = payments.Count > 0;

        // 支払が済んだら「残り」の行にお釣りを出す
        var change = result.ChangeAmount;
        CanConfirm = remaining <= 0;
        RemainingCaption = CanConfirm ? "お釣り" : "残り";
        RemainingText = ViewHelper.Yen(CanConfirm ? change : remaining);
    }

    private async Task AddPaymentAsync(MethodItem item)
    {
        if (remaining <= 0)
        {
            await dialog.InformationAsync("支払は完了しています。");
            return;
        }

        var method = item.Method;
        var tendered = InputValue;
        if (tendered <= 0)
        {
            if (method.Kind == PaymentKind.Cash)
            {
                await dialog.InformationAsync("預り金を入力してください。");
                return;
            }

            // 現金以外は残り全額を既定値にする
            tendered = remaining;
        }

        var amount = Math.Min(tendered, remaining);
        if (!method.AllowsChange && (tendered > remaining))
        {
            // 釣銭を出せない支払方法は残りまで
            tendered = remaining;
        }

        string? reference = null;
        if (method.RequiresReference)
        {
            var input = await popupNavigator.InputReferenceAsync(method.Name);
            if (String.IsNullOrWhiteSpace(input))
            {
                return;
            }

            reference = input.Trim();
        }

        SalesContext.Payments.Add(new CartPayment
        {
            Id = Guid.NewGuid(),
            Method = method,
            Amount = amount,
            TenderedAmount = tendered,
            Reference = reference
        });
        Input.Clear();
        InputText = ViewHelper.Yen(0);
        Refresh();
    }

    protected override Task OnNotifyBackAsync() => Navigator.ForwardAsync(ViewId.Sales);

    protected override Task OnNotifyFunction1() => OnNotifyBackAsync();

    // ポイント利用
    protected override async Task OnNotifyFunction2()
    {
        var customer = SalesContext.Cart.Customer;
        if ((customer is null) || (pointsMethod is null))
        {
            return;
        }

        var existing = SalesContext.Payments.FirstOrDefault(static x => x.Method.Kind == PaymentKind.Points);
        var max = Math.Min(customer.PointBalance, remaining + (existing?.Amount ?? 0m));
        if (max <= 0)
        {
            await dialog.InformationAsync("利用できるポイントがありません。");
            return;
        }

        var text = await popupNavigator.InputPointsAsync($"ポイント (〜{max:#,##0})", (int)(existing?.Amount ?? max));
        if ((text is null) || !Decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out var points))
        {
            return;
        }

        if (points > max)
        {
            await dialog.InformationAsync($"ポイントは {max:#,##0} まで利用できます。");
            return;
        }

        if (existing is not null)
        {
            SalesContext.Payments.Remove(existing);
        }

        if (points > 0)
        {
            SalesContext.Payments.Insert(0, new CartPayment { Id = Guid.NewGuid(), Method = pointsMethod, Amount = points, TenderedAmount = points });
        }

        Refresh();
    }

    protected override Task OnNotifyFunction3() =>
        Navigator.ForwardAsync(ViewId.CustomerSelect, Parameters.Make().WithReturnTo(ViewId.Payment));

    protected override async Task OnNotifyFunction4()
    {
        if (!session.CanTransact)
        {
            await dialog.InformationAsync("レジが開設されていません。");
            return;
        }

        if (remaining > 0)
        {
            return;
        }

        var errors = sales.Validate(SalesContext.Cart, SalesContext.Payments);
        if (errors.Count > 0)
        {
            await dialog.InformationAsync(ViewHelper.Reason(errors[0].Reason));
            return;
        }

        var response = await sales.CompleteAsync(SalesContext.Cart, SalesContext.Payments);
        await Navigator.ForwardAsync(ViewId.Complete, Parameters.Make().WithTransactionId(response.Id));
    }
}
