namespace Pos.Server.Host.Infrastructure.Reports;

using Pos.Server.Accessors;
using Pos.Server.Models;

// 売上集計の groupBy (api-design §3.15)。API の文字列 (camelCase) と管理画面の選択肢で共用
public enum SalesSummaryGroupBy
{
    Day,
    Store,
    Hour,
    Terminal,
    Staff,
    PaymentMethod,
    TaxRate,
    Category
}

// 売上集計の問い合わせ。エンドポイント (JSON / CSV) と管理画面 (S-01 / S-10) で同じ処理を使う
public static class SalesSummaryQuery
{
    private const int DefaultPeriodDays = 30;

    public const string TotalKey = "total";

    private static readonly Dictionary<string, SalesSummaryGroupBy> Keys = Enum.GetValues<SalesSummaryGroupBy>().ToDictionary(ToKey, static x => x, StringComparer.Ordinal);

    // "paymentMethod" など camelCase
    public static string ToKey(SalesSummaryGroupBy groupBy)
    {
        var name = groupBy.ToString();
        return Char.ToLowerInvariant(name[0]) + name[1..];
    }

    public static bool TryParse(string? value, out SalesSummaryGroupBy groupBy)
    {
        if (value is null)
        {
            groupBy = SalesSummaryGroupBy.Day;
            return true;
        }

        return Keys.TryGetValue(value, out groupBy);
    }

    // 営業日の範囲 (両端含む)。指定がなければ to は当日、from は to の 30 日前 (api-design §2.2)
    public static (DateOnly Start, DateOnly End) ResolvePeriod(TimeProvider timeProvider, DateOnly? from, DateOnly? to)
    {
        var end = to ?? DateOnly.FromDateTime(timeProvider.GetLocalNow().Date);
        return (from ?? end.AddDays(-DefaultPeriodDays), end);
    }

    public static async ValueTask<List<SalesSummaryRow>> QueryAsync(
        ReportAccessor accessor,
        StoreAccessor storeAccessor,
        Guid? storeId,
        DateOnly from,
        DateOnly to,
        SalesSummaryGroupBy groupBy,
        CancellationToken cancellationToken) =>
        groupBy switch
        {
            SalesSummaryGroupBy.Store => await accessor.QuerySalesSummaryAsync(storeId, from, to, SalesSummaryGroup.Store, cancellationToken),
            SalesSummaryGroupBy.Hour => await accessor.QuerySalesSummaryByHourAsync(storeId, from, to, await ResolveTimeZoneOffsetAsync(storeAccessor, storeId, from, cancellationToken), cancellationToken),
            SalesSummaryGroupBy.Terminal => await accessor.QuerySalesSummaryAsync(storeId, from, to, SalesSummaryGroup.Terminal, cancellationToken),
            SalesSummaryGroupBy.Staff => await accessor.QuerySalesSummaryAsync(storeId, from, to, SalesSummaryGroup.Staff, cancellationToken),
            SalesSummaryGroupBy.PaymentMethod => await accessor.QuerySalesSummaryByPaymentMethodAsync(storeId, from, to, cancellationToken),
            SalesSummaryGroupBy.TaxRate => await accessor.QuerySalesSummaryByTaxRateAsync(storeId, from, to, cancellationToken),
            SalesSummaryGroupBy.Category => await accessor.QuerySalesSummaryByCategoryAsync(storeId, from, to, cancellationToken),
            _ => await accessor.QuerySalesSummaryAsync(storeId, from, to, SalesSummaryGroup.Day, cancellationToken)
        };

    // 合計行は各行の合算 (CustomerCount は延べ人数)。TaxableAmount / TaxAmount は税率別のときだけ
    public static SalesSummaryRow Sum(IReadOnlyList<SalesSummaryRow> rows, bool withTax)
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

        return new SalesSummaryRow(
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

    // 時間帯集計は店舗のタイムゾーン (storeId なしはサーバのローカル) で TransactedAt を補正する
    public static async ValueTask<string> ResolveTimeZoneOffsetAsync(StoreAccessor storeAccessor, Guid? storeId, DateOnly date, CancellationToken cancellationToken)
    {
        var store = storeId is null ? null : await storeAccessor.QueryAsync(storeId.Value, cancellationToken);
        return ToSqliteOffset(ReportText.ResolveTimeZone(store?.TimeZone), date);
    }

    // SQLite の "+NNN minutes" 修飾子
    public static string ToSqliteOffset(TimeZoneInfo timeZone, DateOnly date)
    {
        var offset = timeZone.GetUtcOffset(date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc));
        return FormattableString.Invariant($"{(int)offset.TotalMinutes:+0;-0} minutes");
    }
}
