namespace Pos.Terminal.Modules.Sales;

// レシート: 32 桁のレシートを画像で表示し、電子レシート QR (レシート番号) と共有 (画像) を提供する
public sealed partial class ReceiptViewModel : AppViewModelBase
{
    private readonly IDialog dialog;

    private ViewId returnTo = ViewId.Complete;

    private Guid transactionId;

    private readonly TransactionUsecase transactions;

    private readonly ReceiptService receipt;

    private byte[] png = [];

    [ObservableProperty]
    public partial string ReceiptNo { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool QrVisible { get; set; }

    [ObservableProperty]
    public partial bool IsRendering { get; set; }

    [ObservableProperty]
    public partial ImageSource? ReceiptImage { get; set; }

    public ReceiptViewModel(
        IDialog dialog,
        TransactionUsecase transactions,
        ReceiptService receipt)
    {
        this.dialog = dialog;
        this.transactions = transactions;
        this.receipt = receipt;
    }

    public override async Task OnNavigatedToAsync(INavigationContext context)
    {
        returnTo = context.Parameter.GetReturnTo(ViewId.Complete);
        var id = context.Parameter.GetTransactionId();
        if (id is null)
        {
            await Navigator.PostForwardAsync(ViewId.Menu);
            return;
        }

        transactionId = id.Value;
        await Navigator.PostActionAsync(LoadAsync);
    }

    private async Task LoadAsync()
    {
        var transaction = await transactions.FindAsync(transactionId);
        if (transaction is null)
        {
            await Navigator.ForwardAsync(ViewId.Menu);
            return;
        }

        ReceiptNo = transaction.ReceiptNo;

        IsRendering = true;
        try
        {
            png = await receipt.BuildAsync(transaction);
        }
        finally
        {
            IsRendering = false;
        }

        ReceiptImage = ImageSource.FromStream(() => new MemoryStream(png));
    }

    protected override Task OnNotifyBackAsync() =>
        Navigator.ForwardAsync(returnTo, Parameters.Make().WithTransactionId(transactionId));

    protected override Task OnNotifyFunction1() => OnNotifyBackAsync();

    protected override async Task OnNotifyFunction2()
    {
        if (png.Length == 0)
        {
            return;
        }

        var path = Path.Combine(FileSystem.CacheDirectory, $"receipt-{ReceiptNo}.png");
        await File.WriteAllBytesAsync(path, png);
        await Share.Default.RequestAsync(new ShareFileRequest { Title = $"レシート {ReceiptNo}", File = new ShareFile(path) });
    }

    protected override async Task OnNotifyFunction4()
    {
        QrVisible = !QrVisible;
        if (QrVisible)
        {
            await dialog.Toast("電子レシート QR (レシート番号)");
        }
    }
}
