namespace Pos.Terminal.Modules.History;

using Pos.Terminal.Models.Entity;

// 種別・送信状態の文言と色は画面側の Converter で付ける
public sealed record TransactionItem(LocalTransactionEntity Entity, TransactionType Type, bool IsVoided, string ReceiptNo, string TotalText, string Detail, OutboxStatus SyncStatus);

// 取引履歴: ローカルの取引を期間・種別で絞り込む。送信状態は Outbox から
public sealed partial class TransactionListViewModel : AppViewModelBase
{
    private static readonly string[] Periods = ["本シフト", "本日", "昨日", "すべて (直近 200 件)"];

    private static readonly string[] Types = ["すべて", "販売", "返品", "取消済み"];

    private readonly IDialog dialog;

    private readonly Session session;

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

    public ObservableCollection<TransactionItem> Items { get; } = [];

    public IObserveCommand PeriodCommand { get; }

    public IObserveCommand TypeCommand { get; }

    public IObserveCommand SelectCommand { get; }

    public TransactionListViewModel(
        IDialog dialog,
        Session session,
        TransactionUsecase transactions,
        SyncService sync)
    {
        this.dialog = dialog;
        this.session = session;
        this.transactions = transactions;
        this.sync = sync;

        PeriodCommand = MakeAsyncCommand(ChoosePeriodAsync);
        TypeCommand = MakeAsyncCommand(async () =>
        {
            var index = await dialog.ChooseAsync(Types, "種別", type);
            if (index >= 0)
            {
                type = index;
                TypeText = Types[index];
                await LoadAsync();
            }
        });
        SelectCommand = MakeAsyncCommand<TransactionItem>(x =>
            Navigator.ForwardAsync(ViewId.TransactionDetail, Parameters.Make().WithTransactionId(x.Entity.Id)));
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
        var index = await dialog.ChooseAsync(Periods, "期間", period);
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
        Items.Replace(list.Select(static x => new TransactionItem(
            x.Transaction,
            x.Transaction.Type,
            x.Transaction.Status == TransactionStatus.Voided,
            x.Transaction.ReceiptNo,
            ViewHelper.Yen(x.Transaction.Total),
            $"{ViewHelper.DateTime(x.Transaction.TransactedAt)}  {(x.Transaction.CustomerId is null ? string.Empty : "👤")}",
            x.SyncStatus)));
        CountText = $"{Items.Count} 件";
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
