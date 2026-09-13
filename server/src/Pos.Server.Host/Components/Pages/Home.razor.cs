namespace Pos.Server.Host.Components.Pages;

using Microsoft.AspNetCore.Components;

using Pos.Server.Accessors;
using Pos.Server.Host.Infrastructure.Api;
using Pos.Server.Host.Infrastructure.Components;
using Pos.Server.Host.Infrastructure.Reports;
using Pos.Server.Models;
using Pos.Server.Models.Entity;

// S-01 ダッシュボード (本日の KPI、店舗別売上、開設中シフト、端末の通信状態、要確認の在庫・会員)
public sealed partial class Home
{
    private const int WarningLimit = 10;

    private DateOnly today;

    private NameLookup names = new();

    private SalesSummaryRow todaySummary = SalesSummaryQuery.Sum([], false);

    private List<SalesSummaryRow> storeRows = [];

    private List<ShiftEntity> openShifts = [];

    private List<TerminalEntity> terminals = [];

    private List<InventoryLevelDetail> negativeInventory = [];

    private long NegativeInventoryCount { get; set; }

    private List<CustomerEntity> negativeCustomers = [];

    [Inject]
    public required ReportAccessor ReportAccessor { get; set; }

    [Inject]
    public required ShiftAccessor ShiftAccessor { get; set; }

    [Inject]
    public required InventoryAccessor InventoryAccessor { get; set; }

    [Inject]
    public required CustomerAccessor CustomerAccessor { get; set; }

    [Inject]
    public required StoreAccessor StoreAccessor { get; set; }

    [Inject]
    public required TerminalAccessor TerminalAccessor { get; set; }

    [Inject]
    public required StaffAccessor StaffAccessor { get; set; }

    private decimal AveragePerCustomer => todaySummary.TransactionCount == 0 ? 0m : Math.Floor(todaySummary.NetSales / todaySummary.TransactionCount);

    private long WarningCount => NegativeInventoryCount + negativeCustomers.Count;

    protected override Task OnInitializedAsync() => LoadAsync();

    private Task LoadAsync() =>
        LoadAsync(async () =>
        {
            today = DateOnly.FromDateTime(TimeProvider.GetLocalNow().Date);
            names = await NameLookup.LoadAsync(StoreAccessor, TerminalAccessor, StaffAccessor, null, CancellationToken);
            storeRows = await SalesSummaryQuery.QueryAsync(ReportAccessor, StoreAccessor, null, today, today, SalesSummaryGroupBy.Store, CancellationToken);
            todaySummary = SalesSummaryQuery.Sum(storeRows, false);
            openShifts = await ShiftAccessor.QueryListAsync(null, null, ShiftStatus.Open, null, null, "OpenedAt", ApiHelper.MaxPageSize, 0, CancellationToken);
            terminals = names.Terminals.Values.Where(static x => !x.IsDeleted && x.IsActive).OrderBy(static x => x.StoreId).ThenBy(static x => x.TerminalNo).ToList();
            NegativeInventoryCount = await InventoryAccessor.CountLevelDetailsAsync(null, null, null, true, CancellationToken);
            negativeInventory = await InventoryAccessor.QueryLevelDetailListAsync(null, null, null, true, "Quantity", WarningLimit, 0, CancellationToken);
            negativeCustomers = await CustomerAccessor.QueryNegativePointListAsync(WarningLimit, CancellationToken);
        });
}
