namespace Pos.Server.Host.Components.Pages;

using Microsoft.AspNetCore.Components;

using MudBlazor;

using Pos.Server.Host.Application.State;
using Pos.Server.Host.Application.Urls;
using Pos.Server.Models.Entity;
using Pos.Server.Models.Views;
using Pos.Server.Services;

// 商品別売上
public sealed partial class ProductSalesPage
{
    private const int RowLimit = 100;

    private List<ProductSalesView> rows = [];
    private List<CategoryEntity> categories = [];
    private DateRange? period;
    private Guid? storeId;
    private Guid? categoryId;
    private ProductSalesSort sort = ProductSalesSort.NetSales;

    [Inject]
    public required ReportService ReportService { get; set; }

    [Inject]
    public required CategoryService CategoryService { get; set; }

    [Inject]
    public required StoreFilterState StoreFilter { get; set; }

    private (DateOnly Start, DateOnly End) Period =>
        ReportService.ResolvePeriod(ToDateOnly(period?.Start), ToDateOnly(period?.End));

    private string CsvUrl => ExportUrls.ProductSalesCsv(Period.Start, Period.End, sort, storeId, categoryId);

    protected override async Task OnInitializedAsync()
    {
        storeId = StoreFilter.StoreId;
        var (start, end) = ReportService.ResolvePeriod(null, null);
        period = new DateRange(start.ToDateTime(TimeOnly.MinValue), end.ToDateTime(TimeOnly.MinValue));
        await LoadAsync(async () =>
        {
            categories = await CategoryService.QueryAllAsync(false, CancellationToken);
        });
        await LoadAsync();
    }

    private static DateOnly? ToDateOnly(DateTime? value) => value is null ? null : DateOnly.FromDateTime(value.Value);

    private Task LoadAsync() =>
        LoadAsync(async () =>
        {
            var (start, end) = Period;
            rows = await ReportService.QueryProductSalesAsync(storeId, start, end, categoryId, sort, RowLimit, CancellationToken);
        });

    private Task OnStoreChanged(Guid? value)
    {
        storeId = value;
        StoreFilter.StoreId = value;
        return LoadAsync();
    }
}
