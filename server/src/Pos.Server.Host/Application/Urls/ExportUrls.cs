namespace Pos.Server.Host.Application.Urls;

using System.Text.Json;

using Microsoft.AspNetCore.WebUtilities;

using Pos.Server.Host.Endpoints;

// 管理画面から開くダウンロード (CSV / PDF) の URL。列挙値は API と同じ camelCase
public static class ExportUrls
{
    public static string ProductsCsv => ApiRoutes.Products + "/csv";

    public static string SalesSummaryCsv(DateOnly from, DateOnly to, SalesSummaryGroupBy groupBy, Guid? storeId) =>
        QueryHelpers.AddQueryString(ApiRoutes.Reports + "/sales/summary/csv", Query(("from", Date(from)), ("to", Date(to)), ("groupBy", Value(groupBy)), ("storeId", storeId?.ToString())));

    public static string ProductSalesCsv(DateOnly from, DateOnly to, ProductSalesSort sort, Guid? storeId, Guid? categoryId) =>
        QueryHelpers.AddQueryString(ApiRoutes.Reports + "/sales/products/csv", Query(("from", Date(from)), ("to", Date(to)), ("sort", Value(sort)), ("storeId", storeId?.ToString()), ("categoryId", categoryId?.ToString())));

    public static string DailySalesPdf(Guid storeId, DateOnly date) =>
        QueryHelpers.AddQueryString(ApiRoutes.Reports + "/sales/daily/pdf", Query(("storeId", storeId.ToString()), ("date", Date(date))));

    public static string ShiftSummaryPdf(Guid shiftId) => $"{ApiRoutes.Shifts}/{shiftId}/summary/pdf";

    public static string ReceiptPdf(Guid transactionId) => $"{ApiRoutes.Transactions}/{transactionId}/receipt/pdf";

    private static string Date(DateOnly value) => value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static string Value<TEnum>(TEnum value)
        where TEnum : struct, Enum =>
        JsonNamingPolicy.CamelCase.ConvertName(value.ToString());

    // null の項目は付けない
    private static IEnumerable<KeyValuePair<string, string?>> Query(params (string Key, string? Value)[] items) =>
        items.Where(static x => x.Value is not null).Select(static x => KeyValuePair.Create(x.Key, x.Value));
}
