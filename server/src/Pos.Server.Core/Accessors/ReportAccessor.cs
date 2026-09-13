namespace Pos.Server.Accessors;

using Pos.Server.Infrastructure.Data;
using Pos.Server.Models;

// 取引テーブルからの集計 (api-design §3.15)。取消済みは除外し、返品は負として扱う
[DataAccessor]
[ExecuteConfig(typeof(DataProfile))]
public sealed partial class ReportAccessor
{
    // groupBy = day / terminal / staff
    [Query]
    public partial ValueTask<List<SalesSummaryRow>> QuerySalesSummaryAsync(Guid? storeId, DateOnly from, DateOnly to, SalesSummaryGroup groupBy, CancellationToken cancellationToken);

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

    // sort は NetSales DESC / NetQuantity DESC など (呼び出し側で検証済み)
    [Query]
    public partial ValueTask<List<ProductSalesRow>> QueryProductSalesAsync(Guid? storeId, DateOnly from, DateOnly to, Guid? categoryId, string sort, int limit, CancellationToken cancellationToken);
}
