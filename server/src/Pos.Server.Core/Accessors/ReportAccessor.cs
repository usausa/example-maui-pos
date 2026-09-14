namespace Pos.Server.Accessors;

using Pos.Server.Models.Parameters;
using Pos.Server.Models.Views;

// 取引テーブルからの集計。取消済みは除外し、返品は負として扱う
[DataAccessor]
[ExecuteConfig(typeof(DataProfile))]
public sealed partial class ReportAccessor
{
    // groupBy = Day / Store / Terminal / Staff
    [Query]
    public partial ValueTask<List<SalesSummaryRow>> QuerySalesSummaryAsync(Guid? storeId, DateOnly from, DateOnly to, SalesSummaryGroupBy groupBy, CancellationToken cancellationToken);

    // timeZoneOffset は SQLite の修飾子 ("+540 minutes" など)。TransactedAt (UTC) を店舗時刻にしてから時間帯で集計する
    [Query]
    public partial ValueTask<List<SalesSummaryRow>> QuerySalesSummaryByHourAsync(Guid? storeId, DateOnly from, DateOnly to, string timeZoneOffset, CancellationToken cancellationToken);

    // 支払額ベース (SalesTotal = 充当額、ReturnsTotal = 返金額)
    [Query]
    public partial ValueTask<List<SalesSummaryRow>> QuerySalesSummaryByPaymentMethodAsync(Guid? storeId, DateOnly from, DateOnly to, CancellationToken cancellationToken);

    // TaxableAmount / TaxAmount 付き
    [Query]
    public partial ValueTask<List<SalesSummaryRow>> QuerySalesSummaryByTaxRateAsync(Guid? storeId, DateOnly from, DateOnly to, CancellationToken cancellationToken);

    [Query]
    public partial ValueTask<List<SalesSummaryRow>> QuerySalesSummaryByCategoryAsync(Guid? storeId, DateOnly from, DateOnly to, CancellationToken cancellationToken);

    [Query]
    public partial ValueTask<List<ProductSalesRow>> QueryProductSalesAsync(Guid? storeId, DateOnly from, DateOnly to, Guid? categoryId, ProductSalesSort sort, int limit, CancellationToken cancellationToken);
}
