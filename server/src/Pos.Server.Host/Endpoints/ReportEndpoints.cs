namespace Pos.Server.Host.Endpoints;

using Pos.Domain.Rules;
using Pos.Server.Accessors;
using Pos.Server.Host.Application;
using Pos.Server.Host.Infrastructure.Api;
using Pos.Server.Host.Infrastructure.Reports;
using Pos.Server.Host.Mappers;
using Pos.Server.Models;
using Pos.Shared.Reports;

// レポート (api-design §3.15)。取引テーブルからの集計で、取消済みは除外し返品は負として扱う
public static class ReportEndpoints
{
    private const string GroupByDay = "day";
    private const string GroupByHour = "hour";
    private const string GroupByTerminal = "terminal";
    private const string GroupByStaff = "staff";
    private const string GroupByPaymentMethod = "paymentMethod";
    private const string GroupByTaxRate = "taxRate";
    private const string GroupByCategory = "category";

    private const string SortNetSales = "netSales";
    private const string SortQuantity = "quantity";

    private const int DefaultProductSize = 50;

    private const int DefaultPeriodDays = 30;

    //--------------------------------------------------------------------------------
    // Mapping
    //--------------------------------------------------------------------------------

    public static void MapReportEndpoints(this WebApplication app)
    {
        var group = app.MapGroup(ApiRoutes.Reports);

        group.MapGet("/sales/summary", HandleSalesSummaryAsync);
        group.MapGet("/sales/products", HandleProductSalesAsync);
        group.MapGet("/sales/daily/pdf", HandleDailySalesPdfAsync);
    }

    //--------------------------------------------------------------------------------
    // Handler
    //--------------------------------------------------------------------------------

    private static async ValueTask<IResult> HandleSalesSummaryAsync(
        ReportAccessor accessor,
        StoreAccessor storeAccessor,
        TimeProvider timeProvider,
        Guid? storeId,
        DateOnly? from,
        DateOnly? to,
        CancellationToken cancellationToken,
        string groupBy = GroupByDay)
    {
        if (!TryResolvePeriod(timeProvider, from, to, out var start, out var end))
        {
            return ApiProblems.Problem(StatusCodes.Status400BadRequest, ErrorCode.ValidationError, "期間の指定が不正です");
        }

        List<SalesSummaryRow> rows;
        switch (groupBy)
        {
            case GroupByDay:
                rows = await accessor.QuerySalesSummaryAsync(storeId, start, end, SalesSummaryGroup.Day, cancellationToken);
                break;
            case GroupByHour:
                rows = await accessor.QuerySalesSummaryByHourAsync(storeId, start, end, await ResolveTimeZoneOffsetAsync(storeAccessor, storeId, start, cancellationToken), cancellationToken);
                break;
            case GroupByTerminal:
                rows = await accessor.QuerySalesSummaryAsync(storeId, start, end, SalesSummaryGroup.Terminal, cancellationToken);
                break;
            case GroupByStaff:
                rows = await accessor.QuerySalesSummaryAsync(storeId, start, end, SalesSummaryGroup.Staff, cancellationToken);
                break;
            case GroupByPaymentMethod:
                rows = await accessor.QuerySalesSummaryByPaymentMethodAsync(storeId, start, end, cancellationToken);
                break;
            case GroupByTaxRate:
                rows = await accessor.QuerySalesSummaryByTaxRateAsync(storeId, start, end, cancellationToken);
                break;
            case GroupByCategory:
                rows = await accessor.QuerySalesSummaryByCategoryAsync(storeId, start, end, cancellationToken);
                break;
            default:
                return ApiProblems.Problem(StatusCodes.Status400BadRequest, ErrorCode.ValidationError, "groupBy が不正です");
        }

        var items = rows.Select(ReportMapper.ToSummaryRow).ToList();
        return TypedResults.Ok(new SalesSummaryResponse
        {
            From = start,
            To = end,
            GroupBy = groupBy,
            Rows = items,
            Total = SumRows(items, groupBy == GroupByTaxRate)
        });
    }

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
        if (!TryResolvePeriod(timeProvider, from, to, out var start, out var end))
        {
            return ApiProblems.Problem(StatusCodes.Status400BadRequest, ErrorCode.ValidationError, "期間の指定が不正です");
        }

        var order = sort switch
        {
            SortNetSales => "NetSales DESC, ProductCode",
            SortQuantity => "NetQuantity DESC, ProductCode",
            _ => null
        };
        if (order is null)
        {
            return ApiProblems.Problem(StatusCodes.Status400BadRequest, ErrorCode.ValidationError, "sort が不正です");
        }

        var rows = await accessor.QueryProductSalesAsync(storeId, start, end, categoryId, order, size, cancellationToken);
        return TypedResults.Ok(new ProductSalesResponse { Rows = rows.Select(ReportMapper.ToProductRow).ToList() });
    }

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
        var byHour = await accessor.QuerySalesSummaryByHourAsync(storeId, date, date, ToSqliteOffset(timeZone, date), cancellationToken);

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

    //--------------------------------------------------------------------------------
    // Helper
    //--------------------------------------------------------------------------------

    // 営業日の範囲 (両端含む)。指定がなければ to は当日、from は to の 30 日前 (api-design §2.2)
    private static bool TryResolvePeriod(TimeProvider timeProvider, DateOnly? from, DateOnly? to, out DateOnly start, out DateOnly end)
    {
        end = to ?? DateOnly.FromDateTime(timeProvider.GetLocalNow().Date);
        start = from ?? end.AddDays(-DefaultPeriodDays);
        return start <= end;
    }

    // 合計行は各行の合算 (customerCount は延べ人数)
    private static SalesSummaryResponseRow SumRows(List<SalesSummaryResponseRow> rows, bool withTax)
    {
        var total = new SalesSummaryResponseRow { Key = "total", Label = "合計" };
        if (withTax)
        {
            total.TaxableAmount = 0;
            total.TaxAmount = 0;
        }

        foreach (var row in rows)
        {
            total.TransactionCount += row.TransactionCount;
            total.ReturnCount += row.ReturnCount;
            total.CustomerCount += row.CustomerCount;
            total.SalesTotal += row.SalesTotal;
            total.ReturnsTotal += row.ReturnsTotal;
            total.NetSales += row.NetSales;
            total.DiscountTotal += row.DiscountTotal;
            total.TaxTotal += row.TaxTotal;
            total.PointsEarned += row.PointsEarned;
            total.PointsRedeemed += row.PointsRedeemed;
            if (withTax)
            {
                total.TaxableAmount += row.TaxableAmount ?? 0;
                total.TaxAmount += row.TaxAmount ?? 0;
            }
        }

        return total;
    }

    // 時間帯集計は店舗のタイムゾーン (storeId なしはサーバのローカル) で TransactedAt を補正する。SQLite の "+NNN minutes" 修飾子
    private static async ValueTask<string> ResolveTimeZoneOffsetAsync(StoreAccessor storeAccessor, Guid? storeId, DateOnly date, CancellationToken cancellationToken)
    {
        var store = storeId is null ? null : await storeAccessor.QueryAsync(storeId.Value, cancellationToken);
        return ToSqliteOffset(ReportText.ResolveTimeZone(store?.TimeZone), date);
    }

    private static string ToSqliteOffset(TimeZoneInfo timeZone, DateOnly date)
    {
        var offset = timeZone.GetUtcOffset(date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc));
        return FormattableString.Invariant($"{(int)offset.TotalMinutes:+0;-0} minutes");
    }
}
