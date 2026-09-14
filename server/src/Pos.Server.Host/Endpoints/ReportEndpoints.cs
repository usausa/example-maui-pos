namespace Pos.Server.Host.Endpoints;

using System.Text.Json;

using Pos.Contract.Reports;
using Pos.Server.Host.Application.Reports;
using Pos.Server.Host.Infrastructure.Api;
using Pos.Server.Host.Models.Export;
using Pos.Server.Models.Parameters;
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
    [MapProperty(nameof(SalesSummaryResponseRow.Key), nameof(SalesSummaryRow.GroupKey))]
    [MapProperty(nameof(SalesSummaryResponseRow.Label), nameof(SalesSummaryRow.GroupLabel))]
    private static partial SalesSummaryResponseRow ToResponse(SalesSummaryRow row);

    [Mapper]
    private static partial ProductSalesResponseRow ToResponse(ProductSalesRow row);

    [Mapper]
    [MapProperty(nameof(SalesSummaryExportRow.Key), nameof(SalesSummaryRow.GroupKey))]
    [MapProperty(nameof(SalesSummaryExportRow.Label), nameof(SalesSummaryRow.GroupLabel))]
    private static partial SalesSummaryExportRow ToExportRow(SalesSummaryRow row);

    [Mapper]
    private static partial ProductSalesExportRow ToExportRow(ProductSalesRow row);

    // "paymentMethod" など camelCase
    private static string ToKey(SalesSummaryGroupBy groupBy) => JsonNamingPolicy.CamelCase.ConvertName(groupBy.ToString());

    // クエリ文字列の列挙値 (大文字小文字は区別しない。省略時は既定)
    private static bool TryParse<TEnum>(string? value, TEnum defaultValue, out TEnum result)
        where TEnum : struct, Enum
    {
        if (String.IsNullOrEmpty(value))
        {
            result = defaultValue;
            return true;
        }

        result = default;
        return !Char.IsDigit(value[0]) && Enum.TryParse(value, true, out result);
    }

    //--------------------------------------------------------------------------------
    // Summary
    //--------------------------------------------------------------------------------

    private static async ValueTask<IResult> HandleSalesSummaryAsync(
        ReportService service,
        Guid? storeId,
        DateOnly? from,
        DateOnly? to,
        string? groupBy,
        CancellationToken cancellationToken)
    {
        var (start, end) = service.ResolvePeriod(from, to);
        if (start > end)
        {
            return ApiProblems.BadRequest("期間の指定が不正です");
        }

        if (!TryParse(groupBy, SalesSummaryGroupBy.Day, out var group))
        {
            return ApiProblems.BadRequest("groupBy が不正です");
        }

        var rows = await service.QuerySalesSummaryAsync(storeId, start, end, group, cancellationToken);
        return TypedResults.Ok(new SalesSummaryResponse
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
        Guid? storeId,
        DateOnly? from,
        DateOnly? to,
        string? groupBy,
        CancellationToken cancellationToken)
    {
        var (start, end) = service.ResolvePeriod(from, to);
        if ((start > end) || !TryParse(groupBy, SalesSummaryGroupBy.Day, out var group))
        {
            return ApiProblems.BadRequest("指定が不正です");
        }

        var rows = await service.QuerySalesSummaryAsync(storeId, start, end, group, cancellationToken);
        rows.Add(ReportService.Sum(rows, group == SalesSummaryGroupBy.TaxRate));
        return CsvExport.Stream(rows.Select(ToExportRow), $"sales-summary-{start:yyyyMMdd}-{end:yyyyMMdd}.csv");
    }

    //--------------------------------------------------------------------------------
    // Products
    //--------------------------------------------------------------------------------

    private static async ValueTask<IResult> HandleProductSalesAsync(
        ReportService service,
        Guid? storeId,
        DateOnly? from,
        DateOnly? to,
        Guid? categoryId,
        string? sort,
        CancellationToken cancellationToken,
        [Range(1, ApiDefaults.MaxPageSize)] int size = ApiDefaults.ProductSalesSize)
    {
        var (start, end) = service.ResolvePeriod(from, to);
        if (start > end)
        {
            return ApiProblems.BadRequest("期間の指定が不正です");
        }

        if (!TryParse(sort, ProductSalesSort.NetSales, out var order))
        {
            return ApiProblems.BadRequest("sort が不正です");
        }

        var rows = await service.QueryProductSalesAsync(storeId, start, end, categoryId, order, size, cancellationToken);
        return TypedResults.Ok(new ProductSalesResponse { Rows = rows.Select(ToResponse).ToList() });
    }

    private static async ValueTask<IResult> HandleProductSalesCsvAsync(
        ReportService service,
        Guid? storeId,
        DateOnly? from,
        DateOnly? to,
        Guid? categoryId,
        string? sort,
        CancellationToken cancellationToken)
    {
        var (start, end) = service.ResolvePeriod(from, to);
        if ((start > end) || !TryParse(sort, ProductSalesSort.NetSales, out var order))
        {
            return ApiProblems.BadRequest("指定が不正です");
        }

        var rows = await service.QueryProductSalesAsync(storeId, start, end, categoryId, order, ApiDefaults.MaxPageSize, cancellationToken);
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
