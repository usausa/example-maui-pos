namespace Pos.Server.Host.Components.Pages;

using Microsoft.AspNetCore.Components;

using MudBlazor;

using Pos.Server.Accessors;
using Pos.Server.Host.Infrastructure.Components;
using Pos.Server.Host.Infrastructure.Reports;
using Pos.Server.Models;

// S-10 売上集計 (グラフ・CSV・売上日報 PDF)
public sealed partial class SalesSummaryPage
{
    // 目盛は 10 本まで
    private readonly BarChartOptions chartOptions = new() { YAxisTicks = 10000, MaxNumYAxisTicks = 10 };

    private List<SalesSummaryRow> rows = [];

    private SalesSummaryRow total = SalesSummaryQuery.Sum([], false);

    private List<ChartSeries<double>> chartSeries = [];

    private string[] chartLabels = [];

    private DateRange? period;

    private Guid? storeId;

    private SalesSummaryGroupBy groupBy = SalesSummaryGroupBy.Day;

    private Guid? reportStoreId;

    private DateTime? reportDate;

    [Inject]
    public required ReportAccessor ReportAccessor { get; set; }

    [Inject]
    public required StoreAccessor StoreAccessor { get; set; }

    [Inject]
    public required StoreFilterState StoreFilter { get; set; }

    private (DateOnly Start, DateOnly End) Period =>
        SalesSummaryQuery.ResolvePeriod(TimeProvider, ToDateOnly(period?.Start), ToDateOnly(period?.End));

    private string CsvUrl =>
        $"api/v1/reports/sales/summary/csv?from={Period.Start:yyyy-MM-dd}&to={Period.End:yyyy-MM-dd}&groupBy={SalesSummaryQuery.ToKey(groupBy)}{(storeId is null ? string.Empty : $"&storeId={storeId}")}";

    private string DailyPdfUrl =>
        (reportStoreId is null) || (reportDate is null) ? string.Empty : $"api/v1/reports/sales/daily/pdf?storeId={reportStoreId}&date={reportDate:yyyy-MM-dd}";

    protected override Task OnInitializedAsync()
    {
        storeId = StoreFilter.StoreId;
        reportStoreId = storeId;
        var today = TimeProvider.GetLocalNow().Date;
        period = new DateRange(today.AddDays(-30), today);
        reportDate = today;
        return LoadAsync();
    }

    private static DateOnly? ToDateOnly(DateTime? value) => value is null ? null : DateOnly.FromDateTime(value.Value);

    private Task LoadAsync() =>
        LoadAsync(async () =>
        {
            var (start, end) = Period;
            rows = await SalesSummaryQuery.QueryAsync(ReportAccessor, StoreAccessor, storeId, start, end, groupBy, CancellationToken);
            total = SalesSummaryQuery.Sum(rows, groupBy == SalesSummaryGroupBy.TaxRate);
            chartLabels = rows.Select(static x => x.GroupLabel).ToArray();
            chartSeries = [new ChartSeries<double> { Name = "純売上", Data = rows.Select(static x => (double)x.NetSales).ToArray() }];
        });

    private Task OnStoreChanged(Guid? value)
    {
        storeId = value;
        StoreFilter.StoreId = value;
        return LoadAsync();
    }
}
