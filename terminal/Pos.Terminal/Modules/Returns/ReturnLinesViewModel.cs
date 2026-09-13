namespace Pos.Terminal.Modules.Returns;

using Pos.Domain.Sales;
using Pos.Shared.Transactions;
using Pos.Terminal.Modules.Navigation.Modal;

public sealed class ReturnLineItem : NotificationObject
{
    public TransactionResponseLine Line { get; }

    public string Name => Line.ProductName;

    public decimal Returnable => Line.Quantity - Line.ReturnedQuantity;

    public bool IsReturnable => Returnable > 0;

    public string Detail => $"{DisplayText.Yen(Line.UnitPrice)} × {DisplayText.Quantity(Line.Quantity)}  返品可 {DisplayText.Quantity(Returnable)}";

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

    public string QuantityText => DisplayText.Quantity(Quantity);

    public ReturnLineItem(TransactionResponseLine line)
    {
        Line = line;
    }
}

// T-41 返品明細選択: 元明細ごとに返品数量 (残数量まで) と理由。返金額と戻るポイントは ReturnCalculator で
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

    private readonly Session session;

    private readonly SalesState sales;

    [ObservableProperty]
    public partial string HeaderText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial IReadOnlyList<ReturnLineItem> Items { get; set; } = [];

    [ObservableProperty]
    public partial string ReasonText { get; set; } = "選択してください";

    [ObservableProperty]
    public partial string RefundText { get; set; } = DisplayText.Yen(0);

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
        Session session,
        SalesState sales)
    {
        this.dialog = dialog;
        this.session = session;
        this.sales = sales;

        DecrementCommand = MakeDelegateCommand<ReturnLineItem>(x => SetQuantity(x, x.Quantity - 1));
        IncrementCommand = MakeDelegateCommand<ReturnLineItem>(x => SetQuantity(x, x.Quantity + 1));
        InputCommand = MakeAsyncCommand<ReturnLineItem>(async x =>
        {
            var text = await popupNavigator.InputNumberAsync($"返品数量 (最大 {DisplayText.Quantity(x.Returnable)})", DisplayText.Quantity(x.Quantity), 4);
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
                sales.ReturnReason = result.Text;
                ReasonText = result.Text;
                Refresh();
            }
        });
    }

    public override async Task OnNavigatedToAsync(INavigationContext context)
    {
        var original = sales.ReturnOriginal;
        if (original is null)
        {
            await Navigator.ForwardAsync(ViewId.Return);
            return;
        }

        HeaderText = $"🧾 {original.ReceiptNo}  {DisplayText.DateTime(original.TransactedAt)}";
        var quantities = sales.ReturnLines.ToDictionary(static x => x.Line.Id, static x => x.Quantity);
        Items = original.Lines.Select(x => new ReturnLineItem(x) { Quantity = quantities.GetValueOrDefault(x.Id) }).ToList();
        ReasonText = sales.ReturnReason ?? "選択してください";
        Refresh();
    }

    private void SetQuantity(ReturnLineItem item, decimal value)
    {
        item.Quantity = Math.Clamp(value, 0, item.Returnable);
        Refresh();
    }

    private void Refresh()
    {
        var original = sales.ReturnOriginal!;
        sales.ReturnLines.Clear();
        foreach (var item in Items.Where(static x => x.Quantity > 0))
        {
            sales.ReturnLines.Add((item.Line, item.Quantity));
        }

        if (sales.ReturnLines.Count == 0)
        {
            RefundText = DisplayText.Yen(0);
            PointsText = string.Empty;
            CanProceed = false;
            return;
        }

        var result = ReturnCalculator.Calculate(TransactionBuilder.ToReturnInput(original, sales.ReturnLines, [], session.TaxRounding));
        RefundText = DisplayText.Yen(result.Total);
        PointsText = (result.PointsEarned != 0) || (result.PointsRedeemed != 0)
            ? $"ポイント: 付与取消 {-result.PointsEarned:#,##0}  返還 {-result.PointsRedeemed:#,##0}"
            : string.Empty;
        CanProceed = sales.ReturnReason is not null;
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
        if (sales.ReturnLines.Count == 0)
        {
            await dialog.InformationAsync("返品する明細を選んでください。");
            return;
        }

        if (sales.ReturnReason is null)
        {
            await dialog.InformationAsync("返品理由を選んでください。");
            return;
        }

        await Navigator.ForwardAsync(ViewId.Refund);
    }
}
