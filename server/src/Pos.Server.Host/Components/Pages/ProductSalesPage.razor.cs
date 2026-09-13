namespace Pos.Server.Host.Components.Pages;

using Microsoft.AspNetCore.Components;

using MudBlazor;

using Pos.Server.Accessors;
using Pos.Server.Host.Endpoints;
using Pos.Server.Host.Infrastructure.Api;
using Pos.Server.Host.Infrastructure.Components;
using Pos.Server.Host.Infrastructure.Data;
using Pos.Server.Host.Infrastructure.Reports;
using Pos.Server.Models;
using Pos.Server.Models.Entity;

// S-11 商品別売上
public sealed partial class ProductSalesPage
{
    private const int RowLimit = 100;

    private List<ProductSalesRow> rows = [];

    private List<CategoryEntity> categories = [];

    private DateRange? period;

    private Guid? storeId;

    private Guid? categoryId;

    private string sort = "netSales";

    [Inject]
    public required ReportAccessor ReportAccessor { get; set; }

    [Inject]
    public required CategoryAccessor CategoryAccessor { get; set; }

    [Inject]
    public required StoreFilterState StoreFilter { get; set; }

    private (DateOnly Start, DateOnly End) Period =>
        SalesSummaryQuery.ResolvePeriod(TimeProvider, ToDateOnly(period?.Start), ToDateOnly(period?.End));

    private string CsvUrl =>
        $"api/v1/reports/sales/products/csv?from={Period.Start:yyyy-MM-dd}&to={Period.End:yyyy-MM-dd}&sort={sort}{(storeId is null ? string.Empty : $"&storeId={storeId}")}{(categoryId is null ? string.Empty : $"&categoryId={categoryId}")}";

    protected override async Task OnInitializedAsync()
    {
        storeId = StoreFilter.StoreId;
        var today = TimeProvider.GetLocalNow().Date;
        period = new DateRange(today.AddDays(-30), today);
        await LoadAsync(async () =>
        {
            categories = CategoryOrder.Sort(await CategoryAccessor.QueryListAsync(null, false, "SortOrder", ApiHelper.MaxPageSize, 0, CancellationToken));
        });
        await LoadAsync();
    }

    private static DateOnly? ToDateOnly(DateTime? value) => value is null ? null : DateOnly.FromDateTime(value.Value);

    // 上位 3 位はメダル
    private static string Rank(int rank) => rank switch
    {
        1 => "🥇",
        2 => "🥈",
        3 => "🥉",
        _ => rank.ToString(CultureInfo.InvariantCulture)
    };

    private Task LoadAsync() =>
        LoadAsync(async () =>
        {
            var (start, end) = Period;
            rows = await ReportAccessor.QueryProductSalesAsync(storeId, start, end, categoryId, ReportEndpoints.ResolveProductSort(sort)!, RowLimit, CancellationToken);
        });

    private Task OnStoreChanged(Guid? value)
    {
        storeId = value;
        StoreFilter.StoreId = value;
        return LoadAsync();
    }
}
