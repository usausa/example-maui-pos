namespace Pos.Server.Services;

using Pos.Server.Accessors;
using Pos.Server.Models.Parameters;
using Pos.Server.Models.Views;

// 売上集計。取引テーブルからの集計で、取消済みは除外し返品は負として扱う
public sealed class ReportService
{
    private const int DefaultPeriodDays = 30;
    public const string TotalKey = "total";

    private readonly MasterAccessor masterAccessor;
    private readonly ReportAccessor reportAccessor;
    private readonly ShiftService shiftService;
    private readonly TimeProvider timeProvider;

    public ReportService(
        MasterAccessor masterAccessor,
        ReportAccessor reportAccessor,
        ShiftService shiftService,
        TimeProvider timeProvider)
    {
        this.masterAccessor = masterAccessor;
        this.reportAccessor = reportAccessor;
        this.shiftService = shiftService;
        this.timeProvider = timeProvider;
    }

    // 今日 (サーバのローカル日付)。期間の既定に使う (ページは時計を持たずこれを使う)
    public DateOnly Today => DateOnly.FromDateTime(timeProvider.GetLocalNow().Date);

    // 営業日の範囲 (両端含む)。指定がなければ to は当日、from は to の 30 日前
    // 省略時の既定: to は今日 (from が未来ならその日)、from は to の 30 日前
    public (DateOnly Start, DateOnly End) ResolvePeriod(DateOnly? from, DateOnly? to)
    {
        var today = Today;
        var end = to ?? ((from > today) ? from.Value : today);
        return (from ?? end.AddDays(-DefaultPeriodDays), end);
    }

    public async ValueTask<List<SalesSummaryView>> QuerySalesSummaryAsync(Guid? storeId, DateOnly from, DateOnly to, SalesSummaryGroupBy groupBy, CancellationToken cancellationToken) =>
        groupBy switch
        {
            SalesSummaryGroupBy.Hour => await reportAccessor.QuerySalesSummaryByHourAsync(storeId, from, to, await ResolveTimeZoneModifierAsync(storeId, from, cancellationToken), cancellationToken),
            SalesSummaryGroupBy.PaymentMethod => await reportAccessor.QuerySalesSummaryByPaymentMethodAsync(storeId, from, to, cancellationToken),
            SalesSummaryGroupBy.TaxRate => await reportAccessor.QuerySalesSummaryByTaxRateAsync(storeId, from, to, cancellationToken),
            SalesSummaryGroupBy.Category => await reportAccessor.QuerySalesSummaryByCategoryAsync(storeId, from, to, cancellationToken),
            _ => await reportAccessor.QuerySalesSummaryAsync(storeId, from, to, groupBy, cancellationToken)
        };

    // 合計行は各行の合算 (CustomerCount は延べ人数)。TaxableAmount / TaxAmount は税率別のときだけ
    public static SalesSummaryView Sum(IEnumerable<SalesSummaryView> rows, bool withTax)
    {
        var transactionCount = 0;
        var returnCount = 0;
        var customerCount = 0;
        var salesTotal = 0m;
        var returnsTotal = 0m;
        var netSales = 0m;
        var discountTotal = 0m;
        var taxTotal = 0m;
        var pointsEarned = 0;
        var pointsRedeemed = 0;
        var taxableAmount = 0m;
        var taxAmount = 0m;
        foreach (var row in rows)
        {
            transactionCount += row.TransactionCount;
            returnCount += row.ReturnCount;
            customerCount += row.CustomerCount;
            salesTotal += row.SalesTotal;
            returnsTotal += row.ReturnsTotal;
            netSales += row.NetSales;
            discountTotal += row.DiscountTotal;
            taxTotal += row.TaxTotal;
            pointsEarned += row.PointsEarned;
            pointsRedeemed += row.PointsRedeemed;
            taxableAmount += row.TaxableAmount ?? 0m;
            taxAmount += row.TaxAmount ?? 0m;
        }

        return new SalesSummaryView(
            TotalKey,
            "合計",
            transactionCount,
            returnCount,
            customerCount,
            salesTotal,
            returnsTotal,
            netSales,
            discountTotal,
            taxTotal,
            pointsEarned,
            pointsRedeemed,
            withTax ? taxableAmount : null,
            withTax ? taxAmount : null);
    }

    public ValueTask<List<ProductSalesView>> QueryProductSalesAsync(Guid? storeId, DateOnly from, DateOnly to, Guid? categoryId, ProductSalesSort sort, int size, CancellationToken cancellationToken) =>
        reportAccessor.QueryProductSalesAsync(storeId, from, to, categoryId, sort, size, cancellationToken);

    // 売上日報 (店舗 × 営業日)。店舗がなければ null
    public async ValueTask<DailySalesReportView?> QueryDailySalesAsync(Guid storeId, DateOnly date, CancellationToken cancellationToken)
    {
        var store = await masterAccessor.QueryStoreAsync(storeId, cancellationToken);
        if (store is null)
        {
            return null;
        }

        var timeZone = StoreService.ResolveTimeZone(store.TimeZone);
        var terminals = (await masterAccessor.QueryTerminalAllAsync(true, cancellationToken)).ToDictionary(static x => x.Id, static x => x.Name);
        var staff = (await masterAccessor.QueryStaffAllAsync(true, cancellationToken)).ToDictionary(static x => x.Id, static x => x.Name);
        var shifts = await shiftService.QueryDetailPageAsync(new ShiftQueryParameter { StoreId = storeId, From = date, To = date, Size = Int32.MaxValue }, cancellationToken);
        return new DailySalesReportView
        {
            StoreCode = store.Code,
            StoreName = store.Name,
            BusinessDate = date,
            TimeZone = timeZone,
            Summary = (await reportAccessor.QuerySalesSummaryAsync(storeId, date, date, SalesSummaryGroupBy.Day, cancellationToken)).FirstOrDefault(),
            ByPaymentMethod = await reportAccessor.QuerySalesSummaryByPaymentMethodAsync(storeId, date, date, cancellationToken),
            ByTaxRate = await reportAccessor.QuerySalesSummaryByTaxRateAsync(storeId, date, date, cancellationToken),
            ByCategory = await reportAccessor.QuerySalesSummaryByCategoryAsync(storeId, date, date, cancellationToken),
            ByHour = await reportAccessor.QuerySalesSummaryByHourAsync(storeId, date, date, ToTimeZoneModifier(timeZone, date), cancellationToken),
            Shifts = shifts.Items.Select(x => new DailySalesReportViewShift(
                terminals.GetValueOrDefault(x.Shift.TerminalId, String.Empty),
                staff.GetValueOrDefault(x.Shift.OpenedByStaffId, String.Empty),
                x.Shift.OpenedAt,
                x.Shift.ClosedAt,
                x.ExpectedCash,
                x.Shift.ActualCash,
                x.Shift.Difference)).ToList()
        };
    }

    // 時間帯集計は店舗のタイムゾーン (storeId なしはサーバのローカル) で TransactedAt を補正する
    private async ValueTask<string> ResolveTimeZoneModifierAsync(Guid? storeId, DateOnly date, CancellationToken cancellationToken)
    {
        var store = storeId is null ? null : await masterAccessor.QueryStoreAsync(storeId.Value, cancellationToken);
        return ToTimeZoneModifier(StoreService.ResolveTimeZone(store?.TimeZone), date);
    }

    // SQLite の日時修飾子 ("+540 minutes" など)。UTC の列を店舗時刻にするときに使う
    private static string ToTimeZoneModifier(TimeZoneInfo timeZone, DateOnly date)
    {
        var offset = timeZone.GetUtcOffset(date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc));
        return FormattableString.Invariant($"{(int)offset.TotalMinutes:+0;-0} minutes");
    }
}
