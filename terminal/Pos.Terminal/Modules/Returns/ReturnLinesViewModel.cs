namespace Pos.Terminal.Modules.Returns;

using Pos.Contract.Transactions;
using Pos.Terminal.Modules.Dialogs;

public sealed class ReturnLineItem : NotificationObject
{
    public TransactionResponseItemLine Line { get; }

    public string Name => Line.ProductName;

    public decimal Returnable => Line.Quantity - Line.ReturnedQuantity;

    public bool IsReturnable => Returnable > 0;

    public string Detail => $"{ViewHelper.Yen(Line.UnitPrice)} × {ViewHelper.Quantity(Line.Quantity)}  返品可 {ViewHelper.Quantity(Returnable)}";

    public decimal Quantity
    {
        get;
        set
        {
            if (SetProperty(ref field, value))
            {
                RaisePropertyChanged(nameof(QuantityText));
            }
        }
    }

    public string QuantityText => ViewHelper.Quantity(Quantity);

    public ReturnLineItem(TransactionResponseItemLine line)
    {
        Line = line;
    }
}

// 返品明細選択: 元明細ごとに返品数量 (残数量まで) と理由。返金額と戻るポイントは ReturnUsecase (ReturnLogic) で
public sealed partial class ReturnLinesViewModel : AppViewModelBase
{
    private static readonly ReasonItem[] Reasons =
    [
        new(null, "不良品"),
        new(null, "お客様都合"),
        new(null, "サイズ・色違い"),
        new(null, "誤登録")
    ];

    private readonly IDialog dialog;

    private readonly ReturnUsecase returns;

    // 返品の画面間で共有する状態 (Scope プラグインが注入する)
    [Scope]
    public ReturnContext ReturnContext { get; set; } = default!;

    [ObservableProperty]
    public partial string HeaderText { get; set; } = string.Empty;

    public ObservableCollection<ReturnLineItem> Items { get; } = [];

    [ObservableProperty]
    public partial string ReasonText { get; set; } = "選択してください";

    [ObservableProperty]
    public partial string RefundText { get; set; } = ViewHelper.Yen(0);

    [ObservableProperty]
    public partial string PointsText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool CanProceed { get; set; }

    public IObserveCommand DecrementCommand { get; }

    public IObserveCommand IncrementCommand { get; }

    public IObserveCommand InputCommand { get; }

    public IObserveCommand ReasonCommand { get; }

    public ReturnLinesViewModel(
        IDialog dialog,
        IPopupNavigator popupNavigator,
        ReturnUsecase returns)
    {
        this.dialog = dialog;
        this.returns = returns;

        DecrementCommand = MakeDelegateCommand<ReturnLineItem>(x => SetQuantity(x, x.Quantity - 1));
        IncrementCommand = MakeDelegateCommand<ReturnLineItem>(x => SetQuantity(x, x.Quantity + 1));
        InputCommand = MakeAsyncCommand<ReturnLineItem>(async x =>
        {
            var text = await popupNavigator.InputQuantityAsync($"返品数量 (最大 {ViewHelper.Quantity(x.Returnable)})", x.Quantity);
            if ((text is not null) && Decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out var value))
            {
                SetQuantity(x, value);
            }
        });
        ReasonCommand = MakeAsyncCommand(async () =>
        {
            var result = await popupNavigator.PopupAsync<ReasonSelectParameter, ReasonSelectResult?>(DialogId.ReasonSelect, new ReasonSelectParameter("返品理由", Reasons, true));
            if (result is not null)
            {
                ReturnContext.Reason = result.Text;
                ReasonText = result.Text;
                Refresh();
            }
        });
    }

    public override async Task OnNavigatedToAsync(INavigationContext context)
    {
        var original = ReturnContext.Original;
        if (original is null)
        {
            await Navigator.PostForwardAsync(ViewId.Return);
            return;
        }

        HeaderText = $"🧾 {original.ReceiptNo}  {ViewHelper.DateTime(original.TransactedAt)}";
        var quantities = ReturnContext.Lines.ToDictionary(static x => x.Line.Id, static x => x.Quantity);
        Items.Replace(original.Lines.Select(x => new ReturnLineItem(x) { Quantity = quantities.GetValueOrDefault(x.Id) }));
        ReasonText = ReturnContext.Reason ?? "選択してください";
        Refresh();
    }

    private void SetQuantity(ReturnLineItem item, decimal value)
    {
        item.Quantity = Math.Clamp(value, 0, item.Returnable);
        Refresh();
    }

    private void Refresh()
    {
        var original = ReturnContext.Original;
        if (original is null)
        {
            return;
        }

        ReturnContext.Lines.Clear();
        foreach (var item in Items.Where(static x => x.Quantity > 0))
        {
            ReturnContext.Lines.Add((item.Line, item.Quantity));
        }

        if (ReturnContext.Lines.Count == 0)
        {
            RefundText = ViewHelper.Yen(0);
            PointsText = string.Empty;
            CanProceed = false;
            return;
        }

        var result = returns.Calculate(original, ReturnContext.Lines, []);
        RefundText = ViewHelper.Yen(result.Total);
        PointsText = (result.PointsEarned != 0) || (result.PointsRedeemed != 0)
            ? $"ポイント: 付与取消 {-result.PointsEarned:#,##0}  返還 {-result.PointsRedeemed:#,##0}"
            : string.Empty;
        CanProceed = ReturnContext.Reason is not null;
    }

    protected override Task OnNotifyBackAsync() => Navigator.ForwardAsync(ViewId.Return);

    protected override Task OnNotifyFunction1() => OnNotifyBackAsync();

    protected override Task OnNotifyFunction2()
    {
        foreach (var item in Items)
        {
            item.Quantity = item.Returnable;
        }

        Refresh();
        return Task.CompletedTask;
    }

    protected override async Task OnNotifyFunction4()
    {
        if (ReturnContext.Lines.Count == 0)
        {
            await dialog.InformationAsync("返品する明細を選んでください。");
            return;
        }

        if (ReturnContext.Reason is null)
        {
            await dialog.InformationAsync("返品理由を選んでください。");
            return;
        }

        await Navigator.ForwardAsync(ViewId.Refund);
    }
}
