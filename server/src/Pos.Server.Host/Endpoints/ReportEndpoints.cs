namespace Pos.Server.Host.Endpoints;

using System.Text.Json;

using Pos.Contract.Reports;
using Pos.Server.Host.Helpers;
using Pos.Server.Host.Infrastructure.Csv;
using Pos.Server.Host.Models.Export;
using Pos.Server.Host.Models.Queries;
using Pos.Server.Host.Reports;
using Pos.Server.Models.Views;
using Pos.Server.Services;

using Smart.Mapper;

// レポート。取引テーブルからの集計で、取消済みは除外し返品は負として扱う
public static partial class ReportEndpoints
{
    //--------------------------------------------------------------------------------
    // Mapping
    //--------------------------------------------------------------------------------

    public static void MapReportEndpoints(this WebApplication app)
    {
        var group = app.MapGroup(ApiRoutes.Reports);
        group.MapGet("/sales/summary", HandleSalesSummaryAsync);
        group.MapGet("/sales/summary/csv", HandleSalesSummaryCsvAsync);
        group.MapGet("/sales/products", HandleProductSalesAsync);
        group.MapGet("/sales/products/csv", HandleProductSalesCsvAsync);
        group.MapGet("/sales/daily/pdf", HandleDailySalesPdfAsync);
    }

    //--------------------------------------------------------------------------------
    // Mapper
    //--------------------------------------------------------------------------------

    [Mapper]
    [MapProperty(nameof(ReportSalesSummaryResponseRow.Key), nameof(SalesSummaryView.GroupKey))]
    [MapProperty(nameof(ReportSalesSummaryResponseRow.Label), nameof(SalesSummaryView.GroupLabel))]
    private static partial ReportSalesSummaryResponseRow ToResponse(SalesSummaryView row);

    [Mapper]
    private static partial ReportProductSalesResponseRow ToResponse(ProductSalesView row);

    [Mapper]
    [MapProperty(nameof(SalesSummaryExportRow.Key), nameof(SalesSummaryView.GroupKey))]
    [MapProperty(nameof(SalesSummaryExportRow.Label), nameof(SalesSummaryView.GroupLabel))]
    private static partial SalesSummaryExportRow ToExportRow(SalesSummaryView row);

    [Mapper]
    private static partial ProductSalesExportRow ToExportRow(ProductSalesView row);

    // "paymentMethod" など camelCase
    private static string ToKey(SalesSummaryGroupBy groupBy) => JsonNamingPolicy.CamelCase.ConvertName(groupBy.ToString());

    //--------------------------------------------------------------------------------
    // Summary
    //--------------------------------------------------------------------------------

    private static async ValueTask<IResult> HandleSalesSummaryAsync(
        ReportService service,
        [AsParameters] ReportPeriodQuery query,
        string? groupBy,
        CancellationToken cancellationToken)
    {
        if (!EnumHelper.TryParse(groupBy, SalesSummaryGroupBy.Day, out var group))
        {
            return ApiProblems.BadRequest("groupBy が不正です");
        }

        var (start, end) = service.ResolvePeriod(query.From, query.To);
        var rows = await service.QuerySalesSummaryAsync(query.StoreId, start, end, group, cancellationToken);
        return TypedResults.Ok(new ReportSalesSummaryResponse
        {
            From = start,
            To = end,
            GroupBy = ToKey(group),
            Rows = rows.Select(ToResponse).ToList(),
            Total = ToResponse(ReportService.Sum(rows, group == SalesSummaryGroupBy.TaxRate))
        });
    }

    private static async ValueTask<IResult> HandleSalesSummaryCsvAsync(
        ReportService service,
        [AsParameters] ReportPeriodQuery query,
        string? groupBy,
        CancellationToken cancellationToken)
    {
        if (!EnumHelper.TryParse(groupBy, SalesSummaryGroupBy.Day, out var group))
        {
            return ApiProblems.BadRequest("groupBy が不正です");
        }

        var (start, end) = service.ResolvePeriod(query.From, query.To);
        var rows = await service.QuerySalesSummaryAsync(query.StoreId, start, end, group, cancellationToken);
        rows.Add(ReportService.Sum(rows, group == SalesSummaryGroupBy.TaxRate));
        return CsvExport.Stream(rows.Select(ToExportRow), $"sales-summary-{start:yyyyMMdd}-{end:yyyyMMdd}.csv");
    }

    //--------------------------------------------------------------------------------
    // Products
    //--------------------------------------------------------------------------------

    private static async ValueTask<IResult> HandleProductSalesAsync(
        ReportService service,
        [AsParameters] ReportPeriodQuery query,
        Guid? categoryId,
        string? sort,
        CancellationToken cancellationToken,
        [Range(1, ApiDefaults.MaxPageSize)] int size = ApiDefaults.ProductSalesSize)
    {
        if (!EnumHelper.TryParse(sort, ProductSalesSort.NetSales, out var order))
        {
            return ApiProblems.BadRequest("sort が不正です");
        }

        var (start, end) = service.ResolvePeriod(query.From, query.To);
        var rows = await service.QueryProductSalesAsync(query.StoreId, start, end, categoryId, order, size, cancellationToken);
        return TypedResults.Ok(new ReportProductSalesResponse { Rows = rows.Select(ToResponse).ToList() });
    }

    private static async ValueTask<IResult> HandleProductSalesCsvAsync(
        ReportService service,
        [AsParameters] ReportPeriodQuery query,
        Guid? categoryId,
        string? sort,
        CancellationToken cancellationToken)
    {
        if (!EnumHelper.TryParse(sort, ProductSalesSort.NetSales, out var order))
        {
            return ApiProblems.BadRequest("sort が不正です");
        }

        var (start, end) = service.ResolvePeriod(query.From, query.To);
        var rows = await service.QueryProductSalesAsync(query.StoreId, start, end, categoryId, order, ApiDefaults.MaxPageSize, cancellationToken);
        return CsvExport.Stream(rows.Select(ToExportRow), $"product-sales-{start:yyyyMMdd}-{end:yyyyMMdd}.csv");
    }

    //--------------------------------------------------------------------------------
    // PDF
    //--------------------------------------------------------------------------------

    // 売上日報 PDF: 店舗 × 営業日
    private static async ValueTask<IResult> HandleDailySalesPdfAsync(
        ReportService service,
        DailySalesReportBuilder reportBuilder,
        Guid storeId,
        DateOnly date,
        CancellationToken cancellationToken)
    {
        var report = await service.QueryDailySalesAsync(storeId, date, cancellationToken);
        if (report is null)
        {
            return ApiProblems.NotFound("店舗が見つかりません");
        }

        var bytes = reportBuilder.Build(report);
        return TypedResults.File(bytes, "application/pdf", $"daily-sales-{report.StoreCode}-{date:yyyyMMdd}.pdf");
    }
}
