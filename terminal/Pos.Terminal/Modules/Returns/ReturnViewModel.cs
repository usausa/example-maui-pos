namespace Pos.Terminal.Modules.Returns;

using Pos.Contract.Transactions;

// 返品: レシート QR / 番号入力 / 履歴から元取引を呼び出す。オンラインならサーバの最新 (返品済数量) を使う
public sealed partial class ReturnViewModel : AppViewModelBase
{
    private readonly IDialog dialog;

    private readonly IPopupNavigator popupNavigator;

    private readonly Session session;

    private readonly ReturnUsecase returns;

    // 返品の画面間で共有する状態 (Scope プラグインが注入する)
    [Scope]
    public ReturnContext ReturnContext { get; set; } = default!;

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
        IPopupNavigator popupNavigator,
        Session session,
        ReturnUsecase returns)
    {
        this.dialog = dialog;
        this.popupNavigator = popupNavigator;
        this.session = session;
        this.returns = returns;

        NextCommand = MakeAsyncCommand(() => Navigator.ForwardAsync(ViewId.ReturnLines), () => CanProceed);
    }

    public override async Task OnNavigatedToAsync(INavigationContext context)
    {
        if (ReturnContext.Original is not null)
        {
            UpdateOriginal(ReturnContext.Original);
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
            ReturnContext.Reset();
            HasOriginal = false;
            CanProceed = false;
            Message = $"❌ 取引が見つかりません: {key}";
            return;
        }

        ReturnContext.Reset();
        ReturnContext.Original = transaction;
        UpdateOriginal(transaction);
    }

    private void UpdateOriginal(TransactionResponseItem transaction)
    {
        HasOriginal = true;
        ReceiptNo = transaction.ReceiptNo;
        TotalText = ViewHelper.Yen(transaction.Total);
        Detail = $"{ViewHelper.DateTime(transaction.TransactedAt)}  {ViewHelper.Name(transaction.Type)} / {ViewHelper.Name(transaction.Status)}";
        Rows.Replace(transaction.Lines.Select(static x => new SummaryRow(
            $"{x.ProductName}\n{ViewHelper.Yen(x.UnitPrice)} × {ViewHelper.Quantity(x.Quantity)}{(x.ReturnedQuantity > 0 ? $"  返品済 {ViewHelper.Quantity(x.ReturnedQuantity)}" : string.Empty)}",
            ViewHelper.Yen(x.NetAmount))));

        var returnable = transaction.IsReturnable();
        CanProceed = returnable;
        Message = returnable
            ? "元取引を確認して、返品する明細を選んでください。"
            : transaction.Type == TransactionType.Return ? "❌ 返品取引は返品できません。" : transaction.Status == TransactionStatus.Voided ? "❌ 取消済みの取引です。" : "❌ すべて返品済みです。";
    }

    protected override Task OnNotifyBackAsync() => Navigator.ForwardAsync(ViewId.Menu);

    protected override Task OnNotifyFunction1() => OnNotifyBackAsync();

    protected override Task OnNotifyFunction2() =>
        Navigator.ForwardAsync(ViewId.Scan, Parameters.Make().WithScan(ScanMode.Receipt, ViewId.Return));

    // レシート番号は自店のものを端末番号 + 連番で入力する (キーボードに依存しない)
    protected override async Task OnNotifyFunction3()
    {
        var text = await popupNavigator.InputReceiptNoAsync();
        if (String.IsNullOrEmpty(text) || (session.Store is null))
        {
            return;
        }

        if (text.Length != Length.ReceiptNoDigits)
        {
            await dialog.InformationAsync("端末番号 2 桁と連番 6 桁を続けて入力してください。");
            return;
        }

        var receiptNo = $"{session.Store.Code}-{text[..2]}-{text[2..]}";
        Apply(await returns.FindOriginalByReceiptNoAsync(receiptNo), receiptNo);
    }

    protected override Task OnNotifyFunction4() => Navigator.ForwardAsync(ViewId.TransactionList);
}
