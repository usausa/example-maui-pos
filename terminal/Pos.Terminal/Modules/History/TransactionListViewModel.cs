namespace Pos.Terminal.Modules.History;

using Pos.Terminal.Models.Entity;

public sealed record TransactionItem(LocalTransactionEntity Entity, string TypeText, Color TypeColor, string ReceiptNo, string TotalText, string Detail, string SyncText, Color SyncColor);

// T-30 取引履歴: ローカルの取引を期間・種別で絞り込む。送信状態は Outbox から
public sealed partial class TransactionListViewModel : AppViewModelBase
{
    private static readonly Color SaleColor = Color.FromArgb("#1E88E5");

    private static readonly Color ReturnColor = Color.FromArgb("#FB8C00");

    private static readonly Color VoidColor = Color.FromArgb("#9E9E9E");

    private static readonly Color SentColor = Color.FromArgb("#43A047");

    private static readonly Color PendingColor = Color.FromArgb("#FB8C00");

    private static readonly Color FailedColor = Color.FromArgb("#E53935");

    private static readonly string[] Periods = ["本シフト", "本日", "昨日", "すべて (直近 200 件)"];

    private static readonly string[] Types = ["すべて", "販売", "返品", "取消済み"];

    private readonly IDialog dialog;

    private readonly DataAccessor accessor;

    private readonly Session session;

    private readonly SyncWorker syncWorker;

    private int period;

    private int type;

    [ObservableProperty]
    public partial string PeriodText { get; set; } = Periods[0];

    [ObservableProperty]
    public partial string TypeText { get; set; } = Types[0];

    [ObservableProperty]
    public partial string CountText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial IReadOnlyList<TransactionItem> Items { get; set; } = [];

    public IObserveCommand PeriodCommand { get; }

    public IObserveCommand TypeCommand { get; }

    public IObserveCommand SelectCommand { get; }

    public TransactionListViewModel(
        IDialog dialog,
        DataAccessor accessor,
        Session session,
        SyncWorker syncWorker)
    {
        this.dialog = dialog;
        this.accessor = accessor;
        this.session = session;
        this.syncWorker = syncWorker;

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

        await LoadAsync();
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

    private async ValueTask LoadAsync()
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

        var list = await accessor.QueryTransactionListAsync(shiftId, businessDate, transactionType, 200);
        if (type == 3)
        {
            list = list.Where(static x => x.Status == TransactionStatus.Voided).ToList();
        }

        // 未送信 (Pending / Failed) の取引
        var outbox = (await accessor.QueryOutboxListAsync(null, 1000))
            .Where(static x => x.Kind is OutboxKind.Transaction or OutboxKind.TransactionVoid)
            .GroupBy(static x => x.TargetId)
            .ToDictionary(static g => g.Key, static g => g.Any(static x => x.Status == OutboxStatus.Failed) ? OutboxStatus.Failed : OutboxStatus.Pending);

        Items = list.Select(x =>
        {
            var voided = x.Status == TransactionStatus.Voided;
            var (syncText, syncColor) = outbox.TryGetValue(x.Id, out var status)
                ? status == OutboxStatus.Failed ? ("⚠ 要確認", FailedColor) : ("⏳ 未送信", PendingColor)
                : ("✓ 送信済", SentColor);
            return new TransactionItem(
                x,
                voided ? "取消" : DisplayText.Name(x.Type),
                voided ? VoidColor : x.Type == TransactionType.Return ? ReturnColor : SaleColor,
                x.ReceiptNo,
                DisplayText.Yen(x.Total),
                $"{DisplayText.DateTime(x.TransactedAt)}  {(x.CustomerId is null ? string.Empty : "👤")}",
                syncText,
                syncColor);
        }).ToList();
        CountText = $"{Items.Count} 件";
    }

    protected override Task OnNotifyBackAsync() => Navigator.ForwardAsync(ViewId.Menu);

    protected override Task OnNotifyFunction1() => OnNotifyBackAsync();

    protected override Task OnNotifyFunction2() => ChoosePeriodAsync();

    protected override async Task OnNotifyFunction4()
    {
        syncWorker.Trigger();
        await dialog.Toast("未送信の取引を再送します。");
    }
}
