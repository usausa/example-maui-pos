namespace Pos.Terminal.Modules.History;

using System.Text.Json;

using Pos.Shared.Transactions;
using Pos.Terminal.Models.Entity;

using Smart.Data;

// T-31 取引詳細: 明細・支払・ポイント・配送。取消は同一シフト内のみ、返品は完了した販売のみ
public sealed partial class TransactionDetailViewModel : AppViewModelBase
{
    private static readonly Color SaleColor = Color.FromArgb("#1E88E5");

    private static readonly Color ReturnColor = Color.FromArgb("#FB8C00");

    private static readonly Color VoidColor = Color.FromArgb("#9E9E9E");

    private static readonly Color SentColor = Color.FromArgb("#43A047");

    private static readonly Color PendingColor = Color.FromArgb("#FB8C00");

    private static readonly Color FailedColor = Color.FromArgb("#E53935");

    private readonly IDialog dialog;

    private readonly IDbProvider provider;

    private readonly DataAccessor accessor;

    private readonly Session session;

    private readonly SyncWorker syncWorker;

    private Guid transactionId;

    private TransactionResponse? transaction;

    [ObservableProperty]
    public partial string TypeText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial Color TypeColor { get; set; } = SaleColor;

    [ObservableProperty]
    public partial string SyncText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial Color SyncColor { get; set; } = SentColor;

    [ObservableProperty]
    public partial string ReceiptNo { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string TotalText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string HeaderDetail { get; set; } = string.Empty;

    [ObservableProperty]
    public partial IReadOnlyList<SummarySection> Sections { get; set; } = [];

    [ObservableProperty]
    public partial bool CanVoid { get; set; }

    [ObservableProperty]
    public partial bool CanReturn { get; set; }

    public TransactionDetailViewModel(
        IDialog dialog,
        IDbProvider provider,
        DataAccessor accessor,
        Session session,
        SyncWorker syncWorker)
    {
        this.dialog = dialog;
        this.provider = provider;
        this.accessor = accessor;
        this.session = session;
        this.syncWorker = syncWorker;
    }

    public override async Task OnNavigatedToAsync(INavigationContext context)
    {
        var id = context.Parameter.GetTransactionId();
        if (id is null)
        {
            await Navigator.ForwardAsync(ViewId.TransactionList);
            return;
        }

        transactionId = id.Value;
        await LoadAsync();
    }

    private async ValueTask LoadAsync()
    {
        var entity = await accessor.QueryTransactionAsync(transactionId);
        transaction = entity is null ? null : JsonSerializer.Deserialize<TransactionResponse>(entity.Payload, HttpService.JsonOptions);
        if (transaction is null)
        {
            await dialog.InformationAsync("取引が見つかりません。");
            await Navigator.ForwardAsync(ViewId.TransactionList);
            return;
        }

        var voided = transaction.Status == TransactionStatus.Voided;
        TypeText = voided ? "取消済み" : DisplayText.Name(transaction.Type);
        TypeColor = voided ? VoidColor : transaction.Type == TransactionType.Return ? ReturnColor : SaleColor;
        ReceiptNo = transaction.ReceiptNo;
        TotalText = DisplayText.Yen(transaction.Total);

        var outbox = (await accessor.QueryOutboxListAsync(null, 1000)).Where(x => x.TargetId == transaction.Id).ToList();
        (SyncText, SyncColor) = outbox.Count == 0 ? ("送信済", SentColor) : outbox.Any(static x => x.Status == OutboxStatus.Failed) ? ("要確認", FailedColor) : ("未送信", PendingColor);

        var staff = await accessor.QueryStaffAsync(transaction.StaffId);
        HeaderDetail = $"{DisplayText.DateTime(transaction.TransactedAt)}  担当 {staff?.Name}  営業日 {DisplayText.Date(transaction.BusinessDate)}";

        var methods = (await accessor.QueryPaymentMethodListAsync()).ToDictionary(static x => x.Id, static x => x.Name);
        var sections = new List<SummarySection>
        {
            new("🛒 明細", transaction.Lines.Select(static x =>
            {
                var discount = x.DiscountAmount + x.AllocatedDiscountAmount;
                var detail = $"{DisplayText.Yen(x.UnitPrice)} × {DisplayText.Quantity(x.Quantity)}{(discount != 0m ? $"  -{DisplayText.Yen(discount)}" : string.Empty)}{(x.ReturnedQuantity > 0 ? $"  返品済 {DisplayText.Quantity(x.ReturnedQuantity)}" : string.Empty)}";
                return new SummaryRow($"{x.ProductName}\n{detail}", DisplayText.Yen(x.NetAmount));
            }).ToList()),
            new("💰 金額",
            [
                new SummaryRow("小計", DisplayText.Yen(transaction.Subtotal)),
                new SummaryRow("値引", "-" + DisplayText.Yen(transaction.DiscountTotal)),
                new SummaryRow("消費税", DisplayText.Yen(transaction.TaxTotal)),
                new SummaryRow("合計", DisplayText.Yen(transaction.Total))
            ]),
            new("💳 支払", transaction.Payments.Select(x => new SummaryRow(
                methods.GetValueOrDefault(x.PaymentMethodId, DisplayText.Name(x.Kind)) + (x.Reference is null ? string.Empty : $" ({x.Reference})"),
                x.TenderedAmount != x.Amount ? $"{DisplayText.Yen(x.Amount)} (預り {DisplayText.Yen(x.TenderedAmount)})" : DisplayText.Yen(x.Amount)))
                .Append(new SummaryRow("お釣り", DisplayText.Yen(transaction.ChangeAmount))).ToList())
        };

        if (transaction.CustomerId is not null)
        {
            sections.Add(new SummarySection("🎁 ポイント",
            [
                new SummaryRow("付与", transaction.PointsEarned.ToString("#,##0", CultureInfo.InvariantCulture)),
                new SummaryRow("利用", transaction.PointsRedeemed.ToString("#,##0", CultureInfo.InvariantCulture)),
                new SummaryRow("残高", transaction.PointsBalanceAfter?.ToString("#,##0", CultureInfo.InvariantCulture) ?? "-")
            ]));
        }

        if (transaction.Delivery is not null)
        {
            var delivery = transaction.Delivery;
            sections.Add(new SummarySection("🚚 配送先",
            [
                new SummaryRow(delivery.RecipientName, delivery.Phone ?? string.Empty),
                new SummaryRow(delivery.Address, delivery.PostalCode ?? string.Empty),
                new SummaryRow("希望", $"{(delivery.RequestedDate is null ? "指定なし" : DisplayText.Date(delivery.RequestedDate.Value))} {delivery.TimeSlot}")
            ]));
        }

        if (transaction.Void is not null)
        {
            var voidStaff = await accessor.QueryStaffAsync(transaction.Void.VoidedByStaffId);
            sections.Add(new SummarySection("🚫 取消",
            [
                new SummaryRow(DisplayText.DateTime(transaction.Void.VoidedAt), voidStaff?.Name ?? string.Empty),
                new SummaryRow("理由", transaction.Void.Reason)
            ]));
        }

        Sections = sections;

        // 取消は同一シフト内の完了取引、返品は完了した販売
        CanVoid = !voided && (session.CurrentShift?.Id == transaction.ShiftId);
        CanReturn = !voided && (transaction.Type == TransactionType.Sale) && session.IsShiftOpen && transaction.Lines.Any(static x => x.Quantity > x.ReturnedQuantity);
    }

    protected override Task OnNotifyBackAsync() => Navigator.ForwardAsync(ViewId.TransactionList);

    protected override Task OnNotifyFunction1() => OnNotifyBackAsync();

    protected override Task OnNotifyFunction2() =>
        Navigator.ForwardAsync(ViewId.Receipt, Parameters.Make().WithReturnTo(ViewId.TransactionDetail).WithTransactionId(transactionId));

    protected override async Task OnNotifyFunction3()
    {
        if ((transaction is null) || (session.Staff is null) || !CanVoid)
        {
            return;
        }

        var reason = await dialog.InputAsync("取消理由");
        if (!reason.Accepted || String.IsNullOrWhiteSpace(reason.Text))
        {
            return;
        }

        if (!await dialog.AskAsync($"{transaction.ReceiptNo} を取り消しますか？\n在庫とポイントが戻ります。", "取消", "取消"))
        {
            return;
        }

        var request = new TransactionVoidRequest { StaffId = session.Staff.Id, Reason = reason.Text.Trim(), VoidedAt = DateTime.UtcNow };
        await TransactionWriter.VoidAsync(provider, accessor, transaction, request);
        await syncWorker.UpdateCountsAsync();
        syncWorker.Trigger();

        await dialog.Toast("取り消しました。");
        await LoadAsync();
    }

    protected override Task OnNotifyFunction4() =>
        Navigator.ForwardAsync(ViewId.Return, Parameters.Make().WithTransactionId(transactionId));
}
