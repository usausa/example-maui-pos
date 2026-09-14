namespace Pos.Terminal.Modules.Sales;

using Pos.Domain.Rules;
using Pos.Domain.Sales;
using Pos.Terminal.Models.Sales;

using Smart.Data;

public sealed record MethodItem(PaymentMethodResponse Method, string Name);

public sealed record PaymentItem(CartPayment Payment, string Text, string AmountText);

// T-20 会計: 埋め込みテンキーで預り金を入れ、支払方法ボタンで支払を積む。確定で Outbox に保存して会計完了へ
public sealed partial class PaymentViewModel : AppViewModelBase
{
    private readonly IDialog dialog;

    private readonly IPopupNavigator popupNavigator;

    private readonly IDbProvider provider;

    private readonly DataAccessor accessor;

    private readonly Settings settings;

    private readonly Session session;

    private readonly SalesState sales;

    private readonly SyncWorker syncWorker;

    private PaymentMethodResponse? pointsMethod;

    private SalesResult result = default!;

    private decimal remaining;

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

    [ObservableProperty]
    public partial IReadOnlyList<PaymentItem> Payments { get; set; } = [];

    [ObservableProperty]
    public partial IReadOnlyList<MethodItem> Methods { get; set; } = [];

    [ObservableProperty]
    public partial string InputText { get; set; } = DisplayText.Yen(0);

    [ObservableProperty]
    public partial bool CanConfirm { get; set; }

    public IObserveCommand PushCommand { get; }

    public IObserveCommand PopCommand { get; }

    public IObserveCommand AddAmountCommand { get; }

    public IObserveCommand ExactCommand { get; }

    public IObserveCommand AddPaymentCommand { get; }

    public IObserveCommand RemovePaymentCommand { get; }

    public PaymentViewModel(
        IDialog dialog,
        IPopupNavigator popupNavigator,
        IDbProvider provider,
        DataAccessor accessor,
        Settings settings,
        Session session,
        SalesState sales,
        SyncWorker syncWorker)
    {
        this.dialog = dialog;
        this.popupNavigator = popupNavigator;
        this.provider = provider;
        this.accessor = accessor;
        this.settings = settings;
        this.session = session;
        this.sales = sales;
        this.syncWorker = syncWorker;

        PushCommand = MakeDelegateCommand<string>(x =>
        {
            Input.Push(x);
            InputText = DisplayText.Yen(InputValue);
        });
        PopCommand = MakeDelegateCommand(() =>
        {
            Input.Pop();
            InputText = DisplayText.Yen(InputValue);
        });
        AddAmountCommand = MakeDelegateCommand<string>(x => SetInput(InputValue + Int32.Parse(x, CultureInfo.InvariantCulture)));
        ExactCommand = MakeDelegateCommand(() => SetInput(Math.Max(0, remaining)));
        AddPaymentCommand = MakeAsyncCommand<MethodItem>(AddPaymentAsync);
        RemovePaymentCommand = MakeDelegateCommand<PaymentItem>(x =>
        {
            sales.Payments.Remove(x.Payment);
            Refresh();
        });
    }

    private decimal InputValue => Decimal.TryParse(Input.NormalizeText, NumberStyles.Number, CultureInfo.InvariantCulture, out var value) ? value : 0m;

    public override async Task OnNavigatedToAsync(INavigationContext context)
    {
        var methods = (await accessor.QueryPaymentMethodListAsync()).Where(static x => x.IsActive && !x.IsDeleted).OrderBy(static x => x.SortOrder).ToList();
        pointsMethod = methods.FirstOrDefault(static x => x.Kind == PaymentKind.Points);
        Methods = methods.Where(static x => x.Kind != PaymentKind.Points).Select(static x => new MethodItem(x, String.IsNullOrEmpty(x.ShortName) ? x.Name : x.ShortName)).ToList();

        // 会員が変わったらポイント支払は無効
        if ((sales.Cart.Customer is null) && sales.Payments.Any(static x => x.Method.Kind == PaymentKind.Points))
        {
            foreach (var payment in sales.Payments.Where(static x => x.Method.Kind == PaymentKind.Points).ToList())
            {
                sales.Payments.Remove(payment);
            }
        }

        Refresh();
    }

    private void SetInput(decimal value)
    {
        Input.Text = value.ToString("0", CultureInfo.InvariantCulture);
        InputText = DisplayText.Yen(value);
    }

    private void Refresh()
    {
        var cart = sales.Cart;
        result = SalesCalculator.Calculate(TransactionBuilder.ToSalesInput(cart, sales.Payments, session.TaxRounding, session.PointBasis));

        var paid = sales.Payments.Sum(static x => x.Amount);
        remaining = result.Total - paid;

        TotalText = DisplayText.Yen(result.Total);
        var customer = cart.Customer;
        var pointsUsed = sales.Payments.Where(static x => x.Method.Kind == PaymentKind.Points).Sum(static x => x.Amount);
        PointsText = customer is null ? "会員なし" : $"{DisplayText.Yen(pointsUsed)} / 残高 {DisplayText.Points(customer.PointBalance)}";
        PointsEnabled = (customer is not null) && (pointsMethod is not null) && (customer.PointBalance > 0);
        PaidText = DisplayText.Yen(paid);
        Payments = sales.Payments.Select(static x => new PaymentItem(x, x.Reference is null ? x.Method.Name : $"{x.Method.Name} {x.Reference}", DisplayText.Yen(x.Amount))).ToList();

        // 支払が済んだら「残り」の行にお釣りを出す
        var change = result.ChangeAmount;
        CanConfirm = remaining <= 0;
        RemainingCaption = CanConfirm ? "お釣り" : "残り";
        RemainingText = DisplayText.Yen(CanConfirm ? change : remaining);
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
            var input = await popupNavigator.InputDigitsAsync($"{method.Name} の伝票番号", string.Empty, 12);
            if (String.IsNullOrWhiteSpace(input))
            {
                return;
            }

            reference = input.Trim();
        }

        sales.Payments.Add(new CartPayment
        {
            Id = Guid.NewGuid(),
            Method = method,
            Amount = amount,
            TenderedAmount = tendered,
            Reference = reference
        });
        Input.Clear();
        InputText = DisplayText.Yen(0);
        Refresh();
    }

    protected override Task OnNotifyBackAsync() => Navigator.ForwardAsync(ViewId.Sales);

    protected override Task OnNotifyFunction1() => OnNotifyBackAsync();

    // ポイント利用
    protected override async Task OnNotifyFunction2()
    {
        var customer = sales.Cart.Customer;
        if ((customer is null) || (pointsMethod is null))
        {
            return;
        }

        var existing = sales.Payments.FirstOrDefault(static x => x.Method.Kind == PaymentKind.Points);
        var max = Math.Min(customer.PointBalance, remaining + (existing?.Amount ?? 0m));
        if (max <= 0)
        {
            await dialog.InformationAsync("利用できるポイントがありません。");
            return;
        }

        var text = await popupNavigator.InputNumberAsync($"ポイント (〜{max:#,##0})", (existing?.Amount ?? max).ToString("0", CultureInfo.InvariantCulture), 7);
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
            sales.Payments.Remove(existing);
        }

        if (points > 0)
        {
            sales.Payments.Insert(0, new CartPayment { Id = Guid.NewGuid(), Method = pointsMethod, Amount = points, TenderedAmount = points });
        }

        Refresh();
    }

    protected override Task OnNotifyFunction3() =>
        Navigator.ForwardAsync(ViewId.CustomerSelect, Parameters.Make().WithReturnTo(ViewId.Payment));

    protected override async Task OnNotifyFunction4()
    {
        if ((settings.StoreId is null) || (settings.TerminalId is null) || (session.Staff is null) || (session.CurrentShift is null))
        {
            await dialog.InformationAsync("レジが開設されていません。");
            return;
        }

        if (remaining > 0)
        {
            return;
        }

        var cart = sales.Cart;
        var input = TransactionBuilder.ToSalesInput(cart, sales.Payments, session.TaxRounding, session.PointBasis);
        var errors = TransactionRules.ValidateInput(input);
        if (errors.Count > 0)
        {
            await dialog.InformationAsync(errors[0].Message);
            return;
        }

        var now = DateTime.UtcNow;
        var receiptNo = await syncWorker.NextReceiptNoAsync();
        var context = new TransactionContext(settings.StoreId.Value, settings.TerminalId.Value, session.Staff.Id, session.CurrentShift.Id, receiptNo, session.BusinessDate, now);
        var request = TransactionBuilder.ToRequest(cart, sales.Payments, result, context);
        var response = TransactionBuilder.ToResponse(request);
        if (cart.Customer is not null)
        {
            response.PointsBalanceAfter = cart.Customer.PointBalance - result.PointsRedeemed + result.PointsEarned;
        }

        await TransactionWriter.SaveAsync(provider, accessor, request, response);
        await syncWorker.UpdateCountsAsync();
        syncWorker.Trigger();

        sales.Completed = response;
        sales.CompletedResult = result;
        await Navigator.ForwardAsync(ViewId.Complete);
    }
}
