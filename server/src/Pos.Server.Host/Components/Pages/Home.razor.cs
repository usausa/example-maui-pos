namespace Pos.Server.Host.Components.Pages;

using Microsoft.AspNetCore.Components;

using Pos.Server.Host.Application.Lookup;
using Pos.Server.Models.Entity;
using Pos.Server.Models.Parameters;
using Pos.Server.Models.Views;
using Pos.Server.Services;

// ダッシュボード (本日の KPI、店舗別売上、引き渡し待ちの受注、開設中シフト、前日までの未締め、端末の通信状態、未受領の移動、要確認の在庫・会員)。
// 取引・シフトなどの変更の通知で読み直し、端末の通信状態は時間で変わるので変更がなくても 1 分ごとに読み直す
public sealed partial class Home
{
    private const int WarningLimit = 10;

    // 通知から読み直すまでの待ち (続けて届く変更を 1 回にまとめる)
    private static readonly TimeSpan RefreshDelay = TimeSpan.FromSeconds(1);

    private static readonly TimeSpan RefreshInterval = TimeSpan.FromMinutes(1);

    private Timer? refreshTimer;

    private DateOnly today;
    private NameLookup names = new();
    private SalesSummaryView todaySummary = ReportService.Sum([], false);
    private List<SalesSummaryView> storeRows = [];
    private IReadOnlyList<ShiftEntity> openShifts = [];
    private List<TerminalEntity> terminals = [];
    private IReadOnlyList<InventoryLevelDetailView> negativeInventory = [];
    private List<CustomerEntity> negativeCustomers = [];
    private IReadOnlyList<DailyClosingDayView> unclosedDays = [];
    private IReadOnlyList<OrderDetailView> arrivedOrders = [];
    private int arrivedOrderCount;
    private int orderedCount;
    private IReadOnlyList<InventoryTransferDetailView> openTransfers = [];
    private int requestedTransferCount;
    private int shippedTransferCount;

    private int NegativeInventoryCount { get; set; }

    private int UnclosedDayCount { get; set; }

    private int OpenTransferCount => requestedTransferCount + shippedTransferCount;

    [Inject]
    public required ChangeNotificationService ChangeNotificationService { get; set; }

    [Inject]
    public required ReportService ReportService { get; set; }

    [Inject]
    public required ShiftService ShiftService { get; set; }

    [Inject]
    public required DailyClosingService DailyClosingService { get; set; }

    [Inject]
    public required OrderService OrderService { get; set; }

    [Inject]
    public required InventoryService InventoryService { get; set; }

    [Inject]
    public required InventoryTransferService InventoryTransferService { get; set; }

    [Inject]
    public required CustomerService CustomerService { get; set; }

    [Inject]
    public required StoreService StoreService { get; set; }

    [Inject]
    public required TerminalService TerminalService { get; set; }

    [Inject]
    public required StaffService StaffService { get; set; }

    private decimal AveragePerCustomer => todaySummary.TransactionCount == 0 ? 0m : Math.Floor(todaySummary.NetSales / todaySummary.TransactionCount);

    private int WarningCount => NegativeInventoryCount + negativeCustomers.Count + UnclosedDayCount;

    protected override Task OnInitializedAsync()
    {
        refreshTimer = new Timer(OnRefreshTimer, null, RefreshInterval, RefreshInterval);
        ChangeNotificationService.Changed += OnDataChanged;
        return LoadAsync();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            ChangeNotificationService.Changed -= OnDataChanged;
            refreshTimer?.Dispose();
            refreshTimer = null;
        }

        base.Dispose(disposing);
    }

    // 書き込みの側 (API の要求) で呼ばれるので、タイマーを掛け直すだけですぐ戻る
    private void OnDataChanged(object? sender, DataChangedEventArgs e) =>
        refreshTimer?.Change(RefreshDelay, RefreshInterval);

    // タイマーのスレッドから回線の同期コンテキストへ移して読み直す
    private void OnRefreshTimer(object? state) => _ = InvokeAsync(RefreshAsync);

    // 読み込み中と、画面を閉じた後は読まない
    private async Task RefreshAsync()
    {
        if (IsBusy || (refreshTimer is null))
        {
            return;
        }

        try
        {
            await LoadAsync();
        }
        catch (OperationCanceledException)
        {
            return;
        }

        StateHasChanged();
    }

    private Task LoadAsync() =>
        LoadAsync(async () =>
        {
            today = ReportService.Today;
            names = await NameLookup.LoadAsync(StoreService, TerminalService, StaffService, null, CancellationToken);
            storeRows = await ReportService.QuerySalesSummaryAsync(null, today, today, SalesSummaryGroupBy.Store, CancellationToken);
            todaySummary = ReportService.Sum(storeRows, false);
            openShifts = (await ShiftService.QueryPageAsync(new ShiftQueryParameter { Status = ShiftStatus.Open, Sort = ShiftSort.OpenedAt, Size = ListLimit }, CancellationToken)).Items;
            terminals = names.Terminals.Values.Where(static x => !x.IsDeleted && x.IsActive).OrderBy(static x => x.StoreId).ThenBy(static x => x.TerminalNo).ToList();
            var negative = await InventoryService.QueryLevelDetailPageAsync(new InventoryLevelDetailQueryParameter { NegativeOnly = true, Sort = InventoryLevelDetailSort.Quantity, Size = WarningLimit }, CancellationToken);
            NegativeInventoryCount = negative.Total;
            negativeInventory = negative.Items;
            negativeCustomers = await CustomerService.QueryNegativePointListAsync(WarningLimit, CancellationToken);
            var unclosed = await DailyClosingService.QueryUnclosedPageAsync(WarningLimit, CancellationToken);
            UnclosedDayCount = unclosed.Total;
            unclosedDays = unclosed.Items;
            var arrived = await OrderService.QueryPageAsync(new OrderQueryParameter { Status = OrderStatus.Arrived, Sort = OrderSort.RequestedDate, Size = WarningLimit }, CancellationToken);
            arrivedOrders = arrived.Items;
            (orderedCount, arrivedOrderCount) = await OrderService.CountOpenAsync(null, CancellationToken);
            openTransfers = (await InventoryTransferService.QueryPageAsync(new InventoryTransferQueryParameter { OpenOnly = true, Sort = InventoryTransferSort.CreatedAt, Desc = false, Size = WarningLimit }, CancellationToken)).Items;
            (requestedTransferCount, shippedTransferCount) = await InventoryTransferService.CountOpenAsync(null, CancellationToken);
        });
}
