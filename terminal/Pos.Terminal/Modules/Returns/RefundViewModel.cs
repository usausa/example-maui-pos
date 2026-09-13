namespace Pos.Terminal.Modules.Returns;

using Pos.Domain.Rules;
using Pos.Domain.Sales;
using Pos.Terminal.Models.Sales;

using Smart.Data;

public sealed class RefundMethodItem : NotificationObject
{
    public PaymentMethodResponse Method { get; }

    public string Name => Method.Name;

    public string Hint { get; }

    public bool IsSelected
    {
        get;
        set
        {
            if (SetProperty(ref field, value))
            {
                RaisePropertyChanged(nameof(Mark));
            }
        }
    }

    public string Mark => IsSelected ? "◉" : "○";

    public RefundMethodItem(PaymentMethodResponse method, string hint)
    {
        Method = method;
        Hint = hint;
    }
}

// T-42 返金: ポイント返還は自動、残りは元の支払方法に応じて返金方法を選ぶ。確定で返品取引を Outbox に保存
public sealed partial class RefundViewModel : AppViewModelBase
{
    private readonly IDialog dialog;

    private readonly IDbProvider provider;

    private readonly DataAccessor accessor;

    private readonly Settings settings;

    private readonly Session session;

    private readonly SalesState sales;

    private readonly SyncWorker syncWorker;

    private SalesResult result = default!;

    private PaymentMethodResponse? pointsMethod;

    private decimal pointsRefund;

    private decimal refund;

    private RefundMethodItem? selected;

    [ObservableProperty]
    public partial string TotalText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string PointsText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string RefundText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial IReadOnlyList<RefundMethodItem> Methods { get; set; } = [];

    [ObservableProperty]
    public partial bool CanConfirm { get; set; }

    public IObserveCommand SelectCommand { get; }

    public RefundViewModel(
        IDialog dialog,
        IDbProvider provider,
        DataAccessor accessor,
        Settings settings,
        Session session,
        SalesState sales,
        SyncWorker syncWorker)
    {
        this.dialog = dialog;
        this.provider = provider;
        this.accessor = accessor;
        this.settings = settings;
        this.session = session;
        this.sales = sales;
        this.syncWorker = syncWorker;

        SelectCommand = MakeDelegateCommand<RefundMethodItem>(Select);
    }

    private void Select(RefundMethodItem item)
    {
        foreach (var x in Methods)
        {
            x.IsSelected = x == item;
        }

        selected = item;
        CanConfirm = (refund == 0) || (selected is not null);
    }

    public override async Task OnNavigatedToAsync(INavigationContext context)
    {
        var original = sales.ReturnOriginal;
        if ((original is null) || (sales.ReturnLines.Count == 0))
        {
            await Navigator.ForwardAsync(ViewId.Return);
            return;
        }

        result = ReturnCalculator.Calculate(TransactionBuilder.ToReturnInput(original, sales.ReturnLines, [], session.TaxRounding));
        pointsRefund = -result.PointsRedeemed;
        refund = result.Total - pointsRefund;

        var methods = (await accessor.QueryPaymentMethodListAsync()).Where(static x => x.IsActive && !x.IsDeleted).OrderBy(static x => x.SortOrder).ToList();
        pointsMethod = methods.FirstOrDefault(static x => x.Kind == PaymentKind.Points);

        TotalText = DisplayText.Yen(result.Total);
        PointsText = pointsRefund > 0 ? $"{pointsRefund:#,##0} pt を返還" : "なし";
        RefundText = DisplayText.Yen(refund);

        // 元取引で使った支払方法を先頭に
        var used = original.Payments.Select(static x => x.PaymentMethodId).ToHashSet();
        Methods = methods
            .Where(static x => x.Kind != PaymentKind.Points)
            .OrderByDescending(x => used.Contains(x.Id))
            .ThenBy(static x => x.SortOrder)
            .Select(x => new RefundMethodItem(x, used.Contains(x.Id) ? "元の支払" : string.Empty))
            .ToList();

        var first = Methods.FirstOrDefault(x => used.Contains(x.Method.Id)) ?? Methods.FirstOrDefault(static x => x.Method.Kind == PaymentKind.Cash);
        if (first is not null)
        {
            Select(first);
        }

        if ((pointsRefund > 0) && (pointsMethod is null))
        {
            await dialog.InformationAsync("ポイント支払方法が登録されていないため、ポイントを返還できません。");
            CanConfirm = false;
        }
    }

    protected override Task OnNotifyBackAsync() => Navigator.ForwardAsync(ViewId.ReturnLines);

    protected override Task OnNotifyFunction1() => OnNotifyBackAsync();

    protected override async Task OnNotifyFunction4()
    {
        var original = sales.ReturnOriginal;
        if ((original is null) || (settings.StoreId is null) || (settings.TerminalId is null) || (session.Staff is null) || (session.CurrentShift is null))
        {
            return;
        }

        var payments = new List<CartPayment>();
        if (pointsRefund > 0)
        {
            payments.Add(new CartPayment { Id = Guid.NewGuid(), Method = pointsMethod!, Amount = pointsRefund, TenderedAmount = pointsRefund });
        }

        if (refund > 0)
        {
            if (selected is null)
            {
                await dialog.InformationAsync("返金方法を選んでください。");
                return;
            }

            payments.Add(new CartPayment { Id = Guid.NewGuid(), Method = selected.Method, Amount = refund, TenderedAmount = refund });
        }

        if (!await dialog.AskAsync($"{DisplayText.Yen(result.Total)} を返金しますか？", "返品", "確定"))
        {
            return;
        }

        var input = TransactionBuilder.ToReturnInput(original, sales.ReturnLines, payments, session.TaxRounding);
        var errors = TransactionRules.ValidateInput(input);
        if (errors.Count > 0)
        {
            await dialog.InformationAsync(errors[0].Message);
            return;
        }

        var calculated = ReturnCalculator.Calculate(input);
        var now = DateTime.UtcNow;
        var receiptNo = await syncWorker.NextReceiptNoAsync();
        var context = new TransactionContext(settings.StoreId.Value, settings.TerminalId.Value, session.Staff.Id, session.CurrentShift.Id, receiptNo, session.BusinessDate, now);
        var request = TransactionBuilder.ToReturnRequest(original, sales.ReturnLines, payments, calculated, context, sales.ReturnReason);
        var response = TransactionBuilder.ToResponse(request);

        await TransactionWriter.SaveAsync(provider, accessor, request, response);
        await syncWorker.UpdateCountsAsync();
        syncWorker.Trigger();

        sales.Completed = response;
        sales.CompletedResult = calculated;
        await Navigator.ForwardAsync(ViewId.Complete);
    }
}
