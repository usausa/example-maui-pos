namespace Pos.Terminal.Modules.Returns;

using Pos.Domain.Logic;
using Pos.Terminal.Models.Cart;

public sealed class RefundMethodItem : NotificationObject
{
    public PaymentMethodResponseItem Method { get; }

    public string Name => Method.Name;

    public string Hint { get; }

    public bool IsSelected
    {
        get;
        set => SetProperty(ref field, value);
    }

    public RefundMethodItem(PaymentMethodResponseItem method, string hint)
    {
        Method = method;
        Hint = hint;
    }
}

// 返金: ポイント返還は自動、残りは元の支払方法に応じて返金方法を選ぶ。確定は ReturnUsecase で返品取引を登録する
public sealed partial class RefundViewModel : AppViewModelBase
{
    private readonly IDialog dialog;

    private readonly Session session;

    private ReturnContext returnContext = new();

    private readonly DataAccessor accessor;

    private readonly ReturnUsecase returns;

    private SalesResult result = default!;

    private PaymentMethodResponseItem? pointsMethod;

    private decimal pointsRefund;

    private decimal refund;

    private RefundMethodItem? selected;

    [ObservableProperty]
    public partial string TotalText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string PointsText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string RefundText { get; set; } = string.Empty;

    public ObservableCollection<RefundMethodItem> Methods { get; } = [];

    [ObservableProperty]
    public partial bool CanConfirm { get; set; }

    public IObserveCommand SelectCommand { get; }

    public RefundViewModel(
        IDialog dialog,
        Session session,
        DataAccessor accessor,
        ReturnUsecase returns)
    {
        this.dialog = dialog;
        this.session = session;
        this.accessor = accessor;
        this.returns = returns;

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
        returnContext = context.Parameter.GetContext<ReturnContext>() ?? new ReturnContext();
        var original = returnContext.Original;
        if ((original is null) || (returnContext.Lines.Count == 0))
        {
            await Navigator.PostForwardAsync(ViewId.Return, Parameters.Make().WithContext(returnContext));
            return;
        }

        result = returns.Calculate(original, returnContext.Lines, []);
        pointsRefund = -result.PointsRedeemed;
        refund = result.Total - pointsRefund;
        TotalText = DisplayText.Yen(result.Total);
        PointsText = pointsRefund > 0 ? $"{pointsRefund:#,##0} pt を返還" : "なし";
        RefundText = DisplayText.Yen(refund);

        await Navigator.PostActionAsync(() => LoadMethodsAsync(original));
    }

    private async Task LoadMethodsAsync(Pos.Contract.Transactions.TransactionResponseItem original)
    {
        var methods = (await accessor.QueryPaymentMethodListAsync()).Where(static x => x.IsActive && !x.IsDeleted).OrderBy(static x => x.SortOrder).ToList();
        pointsMethod = methods.FirstOrDefault(static x => x.Kind == PaymentKind.Points);

        // 元取引で使った支払方法を先頭に
        var used = original.Payments.Select(static x => x.PaymentMethodId).ToHashSet();
        Methods.Replace(methods
            .Where(static x => x.Kind != PaymentKind.Points)
            .OrderByDescending(x => used.Contains(x.Id))
            .ThenBy(static x => x.SortOrder)
            .Select(x => new RefundMethodItem(x, used.Contains(x.Id) ? "元の支払" : string.Empty)));

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

    protected override Task OnNotifyBackAsync() => Navigator.ForwardAsync(ViewId.ReturnLines, Parameters.Make().WithContext(returnContext));

    protected override Task OnNotifyFunction1() => OnNotifyBackAsync();

    protected override async Task OnNotifyFunction4()
    {
        var original = returnContext.Original;
        if ((original is null) || !session.CanTransact)
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

        var errors = returns.Validate(original, returnContext.Lines, payments);
        if (errors.Count > 0)
        {
            await dialog.InformationAsync(RuleText.Of(errors[0].Reason));
            return;
        }

        var response = await returns.CompleteAsync(original, returnContext.Lines, payments, returnContext.Reason);
        await Navigator.ForwardAsync(ViewId.Complete, Parameters.Make().WithTransactionId(response.Id));
    }
}
