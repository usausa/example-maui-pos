namespace Pos.Terminal.Modules.History;

using Pos.Terminal.Models.Entity;

// 取引履歴の 1 件。種別・送信状態の文言と色は画面側の Converter で付け、明細と支払は展開したときに見せる
public sealed class TransactionItem : NotificationObject
{
    public required Guid Id { get; init; }

    public required TransactionType Type { get; init; }

    public required bool IsVoided { get; init; }

    public required string ReceiptNo { get; init; }

    public required string TotalText { get; init; }

    public required string TimeText { get; init; }

    public required string StaffName { get; init; }

    public required string PaymentText { get; init; }

    public required bool HasCustomer { get; init; }

    public required OutboxStatus SyncStatus { get; init; }

    public required IReadOnlyList<SummaryRow> Lines { get; init; }

    public required IReadOnlyList<SummaryRow> Payments { get; init; }

    public bool IsExpanded
    {
        get;
        set => SetProperty(ref field, value);
    }
}

// 取引履歴: ローカルの取引を期間・種別で絞り込む。送信状態は Outbox から
public sealed partial class TransactionListViewModel : AppViewModelBase
{
    private static readonly string[] Periods = ["本シフト", "本日", "昨日", "すべて (直近 200 件)"];

    private static readonly string[] Types = ["すべて", "販売", "返品", "取消済み"];

    private readonly IDialog dialog;

    private readonly IPopupNavigator popupNavigator;

    private readonly Session session;

    private readonly DataAccessor accessor;

    private readonly TransactionUsecase transactions;

    private readonly SyncService sync;

    private int period;

    private int type;

    [ObservableProperty]
    public partial string PeriodText { get; set; } = Periods[0];

    [ObservableProperty]
    public partial string TypeText { get; set; } = Types[0];

    [ObservableProperty]
    public partial string CountText { get; set; } = string.Empty;

    // 件数の内訳 (0 件のチップは画面側で隠す)
    [ObservableProperty]
    public partial int SaleCount { get; set; }

    [ObservableProperty]
    public partial int ReturnCount { get; set; }

    [ObservableProperty]
    public partial int VoidCount { get; set; }

    [ObservableProperty]
    public partial int UnsentCount { get; set; }

    public ObservableCollection<TransactionItem> Items { get; } = [];

    public IObserveCommand PeriodCommand { get; }

    public IObserveCommand TypeCommand { get; }

    public IObserveCommand SelectCommand { get; }

    public IObserveCommand ExpandCommand { get; }

    public TransactionListViewModel(
        IDialog dialog,
        IPopupNavigator popupNavigator,
        Session session,
        DataAccessor accessor,
        TransactionUsecase transactions,
        SyncService sync)
    {
        this.dialog = dialog;
        this.popupNavigator = popupNavigator;
        this.session = session;
        this.accessor = accessor;
        this.transactions = transactions;
        this.sync = sync;

        PeriodCommand = MakeAsyncCommand(ChoosePeriodAsync);
        TypeCommand = MakeAsyncCommand(async () =>
        {
            var index = await popupNavigator.ChooseAsync(Types, "種別", type);
            if (index >= 0)
            {
                type = index;
                TypeText = Types[index];
                await LoadAsync();
            }
        });
        SelectCommand = MakeAsyncCommand<TransactionItem>(x =>
            Navigator.ForwardAsync(ViewId.TransactionDetail, Parameters.Make().WithTransactionId(x.Id)));
        ExpandCommand = MakeDelegateCommand<TransactionItem>(static x => x.IsExpanded = !x.IsExpanded);
    }

    public override async Task OnNavigatedToAsync(INavigationContext context)
    {
        if (!session.IsShiftOpen)
        {
            period = 1;
            PeriodText = Periods[1];
        }

        await Navigator.PostActionAsync(LoadAsync);
    }

    private async Task ChoosePeriodAsync()
    {
        var index = await popupNavigator.ChooseAsync(Periods, "期間", period);
        if (index >= 0)
        {
            period = index;
            PeriodText = Periods[index];
            await LoadAsync();
        }
    }

    private async Task LoadAsync()
    {
        var shiftId = period == 0 ? session.CurrentShift?.Id : null;
        DateOnly? businessDate = period switch
        {
            1 => session.BusinessDate,
            2 => session.BusinessDate.AddDays(-1),
            _ => null
        };
        var transactionType = type switch
        {
            1 => TransactionType.Sale,
            2 => TransactionType.Return,
            _ => (TransactionType?)null
        };

        var list = await transactions.QueryListAsync(shiftId, businessDate, transactionType, type == 3, 200);

        // 担当 (数人) と支払方法の名前はまとめて引く
        var staff = new Dictionary<Guid, string>();
        foreach (var staffId in list.Select(static x => x.Detail.StaffId).Distinct())
        {
            staff[staffId] = (await accessor.QueryStaffAsync(staffId))?.Name ?? "-";
        }
        var methods = (await accessor.QueryPaymentMethodListAsync()).ToDictionary(static x => x.Id, static x => x.Name);

        Items.Replace(list.Select(x => ToItem(x, staff, methods)));
        SaleCount = Items.Count(static x => !x.IsVoided && (x.Type == TransactionType.Sale));
        ReturnCount = Items.Count(static x => !x.IsVoided && (x.Type == TransactionType.Return));
        VoidCount = Items.Count(static x => x.IsVoided);
        UnsentCount = Items.Count(static x => x.SyncStatus != OutboxStatus.Sent);
        CountText = $"{Items.Count} 件";
    }

    private static TransactionItem ToItem(TransactionSummary summary, Dictionary<Guid, string> staff, Dictionary<Guid, string> methods)
    {
        var detail = summary.Detail;
        var payments = detail.Payments
            .Select(x => new SummaryRow(methods.GetValueOrDefault(x.PaymentMethodId) ?? ViewHelper.Name(x.Kind), ViewHelper.Yen(x.Amount)))
            .ToList();
        return new TransactionItem
        {
            Id = summary.Transaction.Id,
            Type = detail.Type,
            IsVoided = detail.Status == TransactionStatus.Voided,
            ReceiptNo = detail.ReceiptNo,
            TotalText = ViewHelper.Yen(detail.Total),
            TimeText = ViewHelper.DateTime(detail.TransactedAt),
            StaffName = staff.GetValueOrDefault(detail.StaffId, "-"),
            PaymentText = payments.Count == 0 ? "-" : String.Join("・", payments.Select(static x => x.Label).Distinct()),
            HasCustomer = detail.CustomerId is not null,
            SyncStatus = summary.SyncStatus,
            Lines = detail.Lines.Select(static x => new SummaryRow($"{x.ProductName} × {ViewHelper.Quantity(x.Quantity)}", ViewHelper.Yen(x.NetAmount))).ToList(),
            Payments = payments
        };
    }

    protected override Task OnNotifyBackAsync() => Navigator.ForwardAsync(ViewId.Menu);

    protected override Task OnNotifyFunction1() => OnNotifyBackAsync();

    protected override Task OnNotifyFunction2() => ChoosePeriodAsync();

    protected override async Task OnNotifyFunction4()
    {
        sync.Trigger();
        await dialog.Toast("未送信の取引を再送します。");
    }
}
