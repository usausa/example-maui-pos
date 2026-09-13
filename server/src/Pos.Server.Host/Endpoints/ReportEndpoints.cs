namespace Pos.Server.Host.Endpoints;

using Pos.Domain.Rules;
using Pos.Server.Accessors;
using Pos.Server.Host.Application;
using Pos.Server.Host.Infrastructure.Api;
using Pos.Server.Host.Infrastructure.Reports;
using Pos.Server.Host.Mappers;
using Pos.Server.Host.Models.Export;
using Pos.Server.Models;
using Pos.Shared.Reports;

// レポート (api-design §3.15)。取引テーブルからの集計で、取消済みは除外し返品は負として扱う
public static class ReportEndpoints
{
    private const string SortNetSales = "netSales";
    private const string SortQuantity = "quantity";

    private const int DefaultProductSize = 50;

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
    // Summary
    //--------------------------------------------------------------------------------

    private static async ValueTask<IResult> HandleSalesSummaryAsync(
        ReportAccessor accessor,
        StoreAccessor storeAccessor,
        TimeProvider timeProvider,
        Guid? storeId,
        DateOnly? from,
        DateOnly? to,
        string? groupBy,
        CancellationToken cancellationToken)
    {
        var (start, end) = SalesSummaryQuery.ResolvePeriod(timeProvider, from, to);
        if (start > end)
        {
            return ApiProblems.Problem(StatusCodes.Status400BadRequest, ErrorCode.ValidationError, "期間の指定が不正です");
        }

        if (!SalesSummaryQuery.TryParse(groupBy, out var group))
        {
            return ApiProblems.Problem(StatusCodes.Status400BadRequest, ErrorCode.ValidationError, "groupBy が不正です");
        }

        var rows = await SalesSummaryQuery.QueryAsync(accessor, storeAccessor, storeId, start, end, group, cancellationToken);
        return TypedResults.Ok(new SalesSummaryResponse
        {
            From = start,
            To = end,
            GroupBy = SalesSummaryQuery.ToKey(group),
            Rows = rows.Select(ReportMapper.ToSummaryRow).ToList(),
            Total = ReportMapper.ToSummaryRow(SalesSummaryQuery.Sum(rows, group == SalesSummaryGroupBy.TaxRate))
        });
    }

    private static async ValueTask<IResult> HandleSalesSummaryCsvAsync(
        ReportAccessor accessor,
        StoreAccessor storeAccessor,
        TimeProvider timeProvider,
        Guid? storeId,
        DateOnly? from,
        DateOnly? to,
        string? groupBy,
        CancellationToken cancellationToken)
    {
        var (start, end) = SalesSummaryQuery.ResolvePeriod(timeProvider, from, to);
        if ((start > end) || !SalesSummaryQuery.TryParse(groupBy, out var group))
        {
            return ApiProblems.Problem(StatusCodes.Status400BadRequest, ErrorCode.ValidationError, "指定が不正です");
        }

        var rows = await SalesSummaryQuery.QueryAsync(accessor, storeAccessor, storeId, start, end, group, cancellationToken);
        rows.Add(SalesSummaryQuery.Sum(rows, group == SalesSummaryGroupBy.TaxRate));
        return CsvExport.Stream(rows.Select(SalesSummaryExportRow.From), $"sales-summary-{start:yyyyMMdd}-{end:yyyyMMdd}.csv");
    }

    //--------------------------------------------------------------------------------
    // Products
    //--------------------------------------------------------------------------------

    private static async ValueTask<IResult> HandleProductSalesAsync(
        ReportAccessor accessor,
        TimeProvider timeProvider,
        Guid? storeId,
        DateOnly? from,
        DateOnly? to,
        Guid? categoryId,
        CancellationToken cancellationToken,
        string sort = SortNetSales,
        [Range(1, ApiHelper.MaxPageSize)] int size = DefaultProductSize)
    {
        var (start, end) = SalesSummaryQuery.ResolvePeriod(timeProvider, from, to);
        if (start > end)
        {
            return ApiProblems.Problem(StatusCodes.Status400BadRequest, ErrorCode.ValidationError, "期間の指定が不正です");
        }

        var order = ResolveProductSort(sort);
        if (order is null)
        {
            return ApiProblems.Problem(StatusCodes.Status400BadRequest, ErrorCode.ValidationError, "sort が不正です");
        }

        var rows = await accessor.QueryProductSalesAsync(storeId, start, end, categoryId, order, size, cancellationToken);
        return TypedResults.Ok(new ProductSalesResponse { Rows = rows.Select(ReportMapper.ToProductRow).ToList() });
    }

    private static async ValueTask<IResult> HandleProductSalesCsvAsync(
        ReportAccessor accessor,
        TimeProvider timeProvider,
        Guid? storeId,
        DateOnly? from,
        DateOnly? to,
        Guid? categoryId,
        CancellationToken cancellationToken,
        string sort = SortNetSales)
    {
        var (start, end) = SalesSummaryQuery.ResolvePeriod(timeProvider, from, to);
        var order = ResolveProductSort(sort);
        if ((start > end) || (order is null))
        {
            return ApiProblems.Problem(StatusCodes.Status400BadRequest, ErrorCode.ValidationError, "指定が不正です");
        }

        var rows = await accessor.QueryProductSalesAsync(storeId, start, end, categoryId, order, ApiHelper.MaxPageSize, cancellationToken);
        return CsvExport.Stream(rows.Select(ProductSalesExportRow.From), $"product-sales-{start:yyyyMMdd}-{end:yyyyMMdd}.csv");
    }

    // sort=netSales|quantity (api-design §3.15)
    public static string? ResolveProductSort(string? sort) =>
        sort switch
        {
            null or SortNetSales => "NetSales DESC, ProductCode",
            SortQuantity => "NetQuantity DESC, ProductCode",
            _ => null
        };

    //--------------------------------------------------------------------------------
    // PDF
    //--------------------------------------------------------------------------------

    // 売上日報 PDF (D-37): 店舗 × 営業日
    private static async ValueTask<IResult> HandleDailySalesPdfAsync(
        ReportAccessor accessor,
        StoreAccessor storeAccessor,
        ShiftAccessor shiftAccessor,
        TerminalAccessor terminalAccessor,
        StaffAccessor staffAccessor,
        DailySalesReportBuilder reportBuilder,
        Guid storeId,
        DateOnly date,
        CancellationToken cancellationToken)
    {
        var store = await storeAccessor.QueryAsync(storeId, cancellationToken);
        if (store is null)
        {
            return ApiProblems.NotFound("店舗が見つかりません");
        }

        var timeZone = ReportText.ResolveTimeZone(store.TimeZone);
        var summary = (await accessor.QuerySalesSummaryAsync(storeId, date, date, SalesSummaryGroup.Day, cancellationToken)).FirstOrDefault();
        var byPaymentMethod = await accessor.QuerySalesSummaryByPaymentMethodAsync(storeId, date, date, cancellationToken);
        var byTaxRate = await accessor.QuerySalesSummaryByTaxRateAsync(storeId, date, date, cancellationToken);
        var byCategory = await accessor.QuerySalesSummaryByCategoryAsync(storeId, date, date, cancellationToken);
        var byHour = await accessor.QuerySalesSummaryByHourAsync(storeId, date, date, SalesSummaryQuery.ToSqliteOffset(timeZone, date), cancellationToken);

        var terminals = (await terminalAccessor.QueryListAsync(storeId, null, true, "TerminalNo", ApiHelper.MaxPageSize, 0, cancellationToken)).ToDictionary(static x => x.Id, static x => x.Name);
        var staff = (await staffAccessor.QueryListAsync(null, null, true, "Code", ApiHelper.MaxPageSize, 0, cancellationToken)).ToDictionary(static x => x.Id, static x => x.Name);
        var shiftEntities = await shiftAccessor.QueryListAsync(storeId, null, null, date, date, "OpenedAt", ApiHelper.MaxPageSize, 0, cancellationToken);
        var shifts = new List<DailySalesReportShift>(shiftEntities.Count);
        foreach (var shift in shiftEntities)
        {
            var expectedCash = shift.Status == ShiftStatus.Closed
                ? shift.ExpectedCash
                : ShiftMapper.ExpectedCash(shift.OpeningCash, await ShiftMapper.ResolveTotalsAsync(shiftAccessor, shift, cancellationToken));
            shifts.Add(new DailySalesReportShift(
                terminals.GetValueOrDefault(shift.TerminalId, String.Empty),
                staff.GetValueOrDefault(shift.OpenedByStaffId, String.Empty),
                shift.OpenedAt,
                shift.ClosedAt,
                expectedCash,
                shift.ActualCash,
                shift.Difference));
        }

        var bytes = reportBuilder.Build(new DailySalesReportData(store.Name, date, timeZone, summary, byPaymentMethod, byTaxRate, byCategory, byHour, shifts));
        return TypedResults.File(bytes, "application/pdf", $"daily-sales-{store.Code}-{date:yyyyMMdd}.pdf");
    }
}
