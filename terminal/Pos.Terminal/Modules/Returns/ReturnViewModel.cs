namespace Pos.Terminal.Modules.Returns;

using System.Text.Json;

using Pos.Shared.Transactions;

// T-40 返品: レシート QR / 番号入力 / 履歴から元取引を呼び出す。オンラインならサーバの最新 (返品済数量) を使う
public sealed partial class ReturnViewModel : AppViewModelBase
{
    private readonly IDialog dialog;

    private readonly DataAccessor accessor;

    private readonly NetworkOperator network;

    private readonly SalesState sales;

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

    [ObservableProperty]
    public partial IReadOnlyList<SummaryRow> Rows { get; set; } = [];

    [ObservableProperty]
    public partial bool CanProceed { get; set; }

    public IObserveCommand NextCommand { get; }

    public ReturnViewModel(
        IDialog dialog,
        DataAccessor accessor,
        NetworkOperator network,
        SalesState sales)
    {
        this.dialog = dialog;
        this.accessor = accessor;
        this.network = network;
        this.sales = sales;

        NextCommand = MakeAsyncCommand(() => Navigator.ForwardAsync(ViewId.ReturnLines), () => CanProceed);
    }

    public override async Task OnNavigatedToAsync(INavigationContext context)
    {
        var id = context.Parameter.GetTransactionId();
        if (id is not null)
        {
            await LoadByIdAsync(id.Value);
        }

        var scanned = context.Parameter.GetScanResult();
        if (scanned is not null)
        {
            await LoadByReceiptNoAsync(scanned);
        }

        if (sales.ReturnOriginal is not null)
        {
            Show(sales.ReturnOriginal);
        }
    }

    private async ValueTask LoadByIdAsync(Guid id)
    {
        TransactionResponse? transaction = null;
        if (network.IsConnected)
        {
            var result = await network.ExecuteAsync(h => h.GetTransactionAsync(id), notify: false);
            transaction = result.Content;
        }

        if (transaction is null)
        {
            var entity = await accessor.QueryTransactionAsync(id);
            transaction = entity is null ? null : JsonSerializer.Deserialize<TransactionResponse>(entity.Payload, HttpService.JsonOptions);
        }

        Apply(transaction, id.ToString());
    }

    private async ValueTask LoadByReceiptNoAsync(string receiptNo)
    {
        TransactionResponse? transaction = null;
        if (network.IsConnected)
        {
            var result = await network.ExecuteAsync(h => h.LookupTransactionAsync(receiptNo), notify: false);
            transaction = result.Content;
        }

        if (transaction is null)
        {
            var entity = await accessor.QueryTransactionByReceiptNoAsync(receiptNo);
            transaction = entity is null ? null : JsonSerializer.Deserialize<TransactionResponse>(entity.Payload, HttpService.JsonOptions);
        }

        Apply(transaction, receiptNo);
    }

    private void Apply(TransactionResponse? transaction, string key)
    {
        if (transaction is null)
        {
            sales.ReturnOriginal = null;
            HasOriginal = false;
            CanProceed = false;
            Message = $"❌ 取引が見つかりません: {key}";
            return;
        }

        sales.ResetReturn();
        sales.ReturnOriginal = transaction;
        Show(transaction);
    }

    private void Show(TransactionResponse transaction)
    {
        HasOriginal = true;
        ReceiptNo = transaction.ReceiptNo;
        TotalText = DisplayText.Yen(transaction.Total);
        Detail = $"{DisplayText.DateTime(transaction.TransactedAt)}  {DisplayText.Name(transaction.Type)} / {DisplayText.Name(transaction.Status)}";
        Rows = transaction.Lines.Select(static x => new SummaryRow(
            $"{x.ProductName}\n{DisplayText.Yen(x.UnitPrice)} × {DisplayText.Quantity(x.Quantity)}{(x.ReturnedQuantity > 0 ? $"  返品済 {DisplayText.Quantity(x.ReturnedQuantity)}" : string.Empty)}",
            DisplayText.Yen(x.NetAmount))).ToList();

        var returnable = (transaction.Type == TransactionType.Sale) && (transaction.Status == TransactionStatus.Completed) && transaction.Lines.Any(static x => x.Quantity > x.ReturnedQuantity);
        CanProceed = returnable;
        Message = returnable
            ? "元取引を確認して、返品する明細を選んでください。"
            : transaction.Type == TransactionType.Return ? "❌ 返品取引は返品できません。" : transaction.Status == TransactionStatus.Voided ? "❌ 取消済みの取引です。" : "❌ すべて返品済みです。";
    }

    protected override Task OnNotifyBackAsync()
    {
        sales.ResetReturn();
        return Navigator.ForwardAsync(ViewId.Menu);
    }

    protected override Task OnNotifyFunction1() => OnNotifyBackAsync();

    protected override Task OnNotifyFunction2() =>
        Navigator.ForwardAsync(ViewId.Scan, Parameters.Make().WithScan(ScanMode.Receipt, ViewId.Return));

    protected override async Task OnNotifyFunction3()
    {
        var result = await dialog.InputAsync("レシート番号", placeHolder: "S001-01-000123");
        if (result.Accepted && !String.IsNullOrWhiteSpace(result.Text))
        {
            await LoadByReceiptNoAsync(result.Text.Trim());
        }
    }

    protected override Task OnNotifyFunction4() => Navigator.ForwardAsync(ViewId.TransactionList);
}
