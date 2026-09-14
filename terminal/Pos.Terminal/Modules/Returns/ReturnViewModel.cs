namespace Pos.Terminal.Modules.Returns;

using Pos.Contract.Transactions;

// 返品: レシート QR / 番号入力 / 履歴から元取引を呼び出す。オンラインならサーバの最新 (返品済数量) を使う
public sealed partial class ReturnViewModel : AppViewModelBase
{
    private readonly IDialog dialog;

    private ReturnContext returnContext = new();

    private readonly ReturnUsecase returns;

    [ObservableProperty]
    public partial string Message { get; set; } = "レシートの QR をスキャンするか、レシート番号を入力してください。";

    [ObservableProperty]
    public partial bool HasOriginal { get; set; }

    [ObservableProperty]
    public partial string ReceiptNo { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string TotalText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Detail { get; set; } = string.Empty;

    public ObservableCollection<SummaryRow> Rows { get; } = [];

    [ObservableProperty]
    public partial bool CanProceed { get; set; }

    public IObserveCommand NextCommand { get; }

    public ReturnViewModel(
        IDialog dialog,
        ReturnUsecase returns)
    {
        this.dialog = dialog;
        this.returns = returns;

        NextCommand = MakeAsyncCommand(() => Navigator.ForwardAsync(ViewId.ReturnLines, Parameters.Make().WithContext(returnContext)), () => CanProceed);
    }

    public override async Task OnNavigatedToAsync(INavigationContext context)
    {
        returnContext = context.Parameter.GetContext<ReturnContext>() ?? new ReturnContext();
        if (returnContext.Original is not null)
        {
            UpdateOriginal(returnContext.Original);
        }

        var id = context.Parameter.GetTransactionId();
        var scanned = context.Parameter.GetScanResult();
        if (id is not null)
        {
            await Navigator.PostActionAsync(async () => Apply(await returns.FindOriginalAsync(id.Value), id.Value.ToString()));
        }
        else if (scanned is not null)
        {
            await Navigator.PostActionAsync(async () => Apply(await returns.FindOriginalByReceiptNoAsync(scanned), scanned));
        }
    }

    private void Apply(TransactionResponseItem? transaction, string key)
    {
        if (transaction is null)
        {
            returnContext.Reset();
            HasOriginal = false;
            CanProceed = false;
            Message = $"❌ 取引が見つかりません: {key}";
            return;
        }

        returnContext.Reset();
        returnContext.Original = transaction;
        UpdateOriginal(transaction);
    }

    private void UpdateOriginal(TransactionResponseItem transaction)
    {
        HasOriginal = true;
        ReceiptNo = transaction.ReceiptNo;
        TotalText = DisplayText.Yen(transaction.Total);
        Detail = $"{DisplayText.DateTime(transaction.TransactedAt)}  {DisplayText.Name(transaction.Type)} / {DisplayText.Name(transaction.Status)}";
        Rows.Replace(transaction.Lines.Select(static x => new SummaryRow(
            $"{x.ProductName}\n{DisplayText.Yen(x.UnitPrice)} × {DisplayText.Quantity(x.Quantity)}{(x.ReturnedQuantity > 0 ? $"  返品済 {DisplayText.Quantity(x.ReturnedQuantity)}" : string.Empty)}",
            DisplayText.Yen(x.NetAmount))));

        var returnable = transaction.IsReturnable();
        CanProceed = returnable;
        Message = returnable
            ? "元取引を確認して、返品する明細を選んでください。"
            : transaction.Type.IsReturn() ? "❌ 返品取引は返品できません。" : transaction.Status.IsVoided() ? "❌ 取消済みの取引です。" : "❌ すべて返品済みです。";
    }

    protected override Task OnNotifyBackAsync() => Navigator.ForwardAsync(ViewId.Menu);

    protected override Task OnNotifyFunction1() => OnNotifyBackAsync();

    protected override Task OnNotifyFunction2() =>
        Navigator.ForwardAsync(ViewId.Scan, Parameters.Make().WithScan(ScanMode.Receipt, ViewId.Return).WithContext(returnContext));

    protected override async Task OnNotifyFunction3()
    {
        var result = await dialog.InputAsync("レシート番号", placeHolder: "S001-01-000123");
        if (result.Accepted && !String.IsNullOrWhiteSpace(result.Text))
        {
            var receiptNo = result.Text.Trim();
            Apply(await returns.FindOriginalByReceiptNoAsync(receiptNo), receiptNo);
        }
    }

    protected override Task OnNotifyFunction4() => Navigator.ForwardAsync(ViewId.TransactionList);
}
