namespace Pos.Terminal.Modules.Sales;

using System.Text.Json;

using Pos.Shared.Transactions;

// T-22 レシート: 32 桁のレシートを画像で表示し、電子レシート QR (レシート番号) と共有 (画像) を提供する
public sealed partial class ReceiptViewModel : AppViewModelBase
{
    private readonly IDialog dialog;

    private readonly DataAccessor accessor;

    private readonly Session session;

    private readonly SalesState sales;

    private ViewId returnTo = ViewId.Complete;

    private Guid? transactionId;

    private byte[] png = [];

    [ObservableProperty]
    public partial string ReceiptNo { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool QrVisible { get; set; }

    [ObservableProperty]
    public partial ImageSource? ReceiptImage { get; set; }

    public ReceiptViewModel(
        IDialog dialog,
        DataAccessor accessor,
        Session session,
        SalesState sales)
    {
        this.dialog = dialog;
        this.accessor = accessor;
        this.session = session;
        this.sales = sales;
    }

    public override async Task OnNavigatedToAsync(INavigationContext context)
    {
        returnTo = context.Parameter.GetReturnTo(ViewId.Complete);
        transactionId = context.Parameter.GetTransactionId();

        // 会計完了からは直前の取引、履歴からは指定の取引
        var transaction = sales.Completed;
        if (transactionId is not null)
        {
            var entity = await accessor.QueryTransactionAsync(transactionId.Value);
            transaction = entity is null ? null : JsonSerializer.Deserialize<TransactionResponse>(entity.Payload, HttpService.JsonOptions);
        }

        if (transaction is null)
        {
            await Navigator.ForwardAsync(ViewId.Menu);
            return;
        }

        ReceiptNo = transaction.ReceiptNo;

        var staff = await accessor.QueryStaffAsync(transaction.StaffId);
        var methods = (await accessor.QueryPaymentMethodListAsync()).ToDictionary(static x => x.Id, static x => x.Name);
        var text = ReceiptFormatter.Format(transaction, session.Store, session.Terminal?.Name ?? string.Empty, staff?.Name ?? string.Empty, methods);
        png = ReceiptRenderer.RenderPng(text);
        ReceiptImage = ImageSource.FromStream(() => new MemoryStream(png));
    }

    protected override Task OnNotifyBackAsync() =>
        transactionId is null
            ? Navigator.ForwardAsync(returnTo)
            : Navigator.ForwardAsync(returnTo, Parameters.Make().WithTransactionId(transactionId.Value));

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
