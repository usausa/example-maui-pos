namespace Pos.Server.Host.Components.Pages;

using Microsoft.AspNetCore.Components;

using MudBlazor;

using Pos.Server.Host.Application.State;
using Pos.Server.Host.Application.Urls;
using Pos.Server.Models.Views;
using Pos.Server.Services;

// 売上集計 (グラフ・CSV・売上日報 PDF)
public sealed partial class SalesSummaryPage
{
    // 目盛は 10 本まで
    private readonly BarChartOptions chartOptions = new() { YAxisTicks = 10000, MaxNumYAxisTicks = 10 };

    private List<SalesSummaryView> rows = [];
    private SalesSummaryView total = ReportService.Sum([], false);
    private List<ChartSeries<double>> chartSeries = [];
    private string[] chartLabels = [];
    private DateRange? period;
    private Guid? storeId;
    private SalesSummaryGroupBy groupBy = SalesSummaryGroupBy.Day;
    private Guid? reportStoreId;
    private DateTime? reportDate;

    [Inject]
    public required ReportService ReportService { get; set; }

    [Inject]
    public required StoreFilterState StoreFilter { get; set; }

    private (DateOnly Start, DateOnly End) Period =>
        ReportService.ResolvePeriod(ToDateOnly(period?.Start), ToDateOnly(period?.End));

    private string CsvUrl => ExportUrls.SalesSummaryCsv(Period.Start, Period.End, groupBy, storeId);

    private string DailyPdfUrl =>
        (reportStoreId is null) || (reportDate is null) ? string.Empty : ExportUrls.DailySalesPdf(reportStoreId.Value, DateOnly.FromDateTime(reportDate.Value));

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
            rows = await ReportService.QuerySalesSummaryAsync(storeId, start, end, groupBy, CancellationToken);
            total = ReportService.Sum(rows, groupBy == SalesSummaryGroupBy.TaxRate);
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
