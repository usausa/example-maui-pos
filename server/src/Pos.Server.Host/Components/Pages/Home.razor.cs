namespace Pos.Server.Host.Components.Pages;

using Microsoft.AspNetCore.Components;

using Pos.Server.Models.Entity;
using Pos.Server.Models.Parameters;
using Pos.Server.Models.Views;
using Pos.Server.Services;

// S-01 ダッシュボード (本日の KPI、店舗別売上、開設中シフト、端末の通信状態、要確認の在庫・会員)
public sealed partial class Home
{
    private const int WarningLimit = 10;

    private DateOnly today;
    private NameLookup names = new();
    private SalesSummaryRow todaySummary = ReportService.Sum([], false);
    private List<SalesSummaryRow> storeRows = [];
    private IReadOnlyList<ShiftEntity> openShifts = [];
    private List<TerminalEntity> terminals = [];
    private IReadOnlyList<InventoryLevelDetail> negativeInventory = [];
    private List<CustomerEntity> negativeCustomers = [];

    private int NegativeInventoryCount { get; set; }

    [Inject]
    public required ReportService ReportService { get; set; }

    [Inject]
    public required ShiftService ShiftService { get; set; }

    [Inject]
    public required InventoryService InventoryService { get; set; }

    [Inject]
    public required CustomerService CustomerService { get; set; }

    [Inject]
    public required StoreService StoreService { get; set; }

    [Inject]
    public required TerminalService TerminalService { get; set; }

    [Inject]
    public required StaffService StaffService { get; set; }

    private decimal AveragePerCustomer => todaySummary.TransactionCount == 0 ? 0m : Math.Floor(todaySummary.NetSales / todaySummary.TransactionCount);

    private int WarningCount => NegativeInventoryCount + negativeCustomers.Count;

    protected override Task OnInitializedAsync() => LoadAsync();

    private Task LoadAsync() =>
        LoadAsync(async () =>
        {
            today = DateOnly.FromDateTime(TimeProvider.GetLocalNow().Date);
            names = await NameLookup.LoadAsync(StoreService, TerminalService, StaffService, null, CancellationToken);
            storeRows = await ReportService.QuerySalesSummaryAsync(null, today, today, SalesSummaryGroupBy.Store, CancellationToken);
            todaySummary = ReportService.Sum(storeRows, false);
            openShifts = (await ShiftService.QueryPageAsync(new ShiftQueryParameter { Status = ShiftStatus.Open, Sort = "OpenedAt", Size = ListLimit }, CancellationToken)).Items;
            terminals = names.Terminals.Values.Where(static x => !x.IsDeleted && x.IsActive).OrderBy(static x => x.StoreId).ThenBy(static x => x.TerminalNo).ToList();
            var negative = await InventoryService.QueryLevelDetailPageAsync(new InventoryLevelDetailQueryParameter { NegativeOnly = true, Sort = "Quantity", Size = WarningLimit }, CancellationToken);
            NegativeInventoryCount = negative.Total;
            negativeInventory = negative.Items;
            negativeCustomers = await CustomerService.QueryNegativePointListAsync(WarningLimit, CancellationToken);
        });
}
