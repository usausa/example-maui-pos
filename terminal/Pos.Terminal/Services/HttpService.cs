namespace Pos.Terminal.Services;

using System.Net.Http.Json;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Unicode;

using Pos.Contract.Customers;
using Pos.Contract.Inventory;
using Pos.Contract.Reports;
using Pos.Contract.Shifts;
using Pos.Contract.Sync;
using Pos.Contract.Transactions;
using Pos.Terminal.Helpers.Json;

// 端末が使うサーバ API。JSON はサーバと同じ契約 (camelCase / null 省略 / 列挙型は文字列 / 日時は UTC)
public sealed class HttpService
{
    private const string Prefix = "api/v1/";

    // IHttpClientFactory が返すクライアントはハンドラがプール管理されるため Dispose 不要
    private readonly IHttpClientFactory httpClientFactory;

    public static JsonSerializerOptions JsonOptions { get; } = CreateJsonOptions();

    public HttpService(IHttpClientFactory httpClientFactory)
    {
        this.httpClientFactory = httpClientFactory;
    }

    public static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            Encoder = JavaScriptEncoder.Create(UnicodeRanges.All)
        };
        options.Converters.Add(new JsonDateTimeConverter());
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }

    //--------------------------------------------------------------------------------
    // Master
    //--------------------------------------------------------------------------------

    public ValueTask<ApiResult<StoreResponseItem>> GetStoreAsync(Guid id, CancellationToken cancellationToken = default) =>
        GetAsync<StoreResponseItem>($"stores/{id}", cancellationToken);

    public ValueTask<ApiResult<TerminalResponseItem>> GetTerminalAsync(Guid id, CancellationToken cancellationToken = default) =>
        GetAsync<TerminalResponseItem>($"terminals/{id}", cancellationToken);

    public ValueTask<ApiResult<SyncMastersResponse>> GetSyncMastersAsync(DateTime? since, CancellationToken cancellationToken = default) =>
        GetAsync<SyncMastersResponse>(since is null ? "sync/masters" : $"sync/masters?since={Format(since.Value)}", cancellationToken);

    public ValueTask<ApiResult<ProductResponse>> GetProductsAsync(DateTime? updatedSince, int page, int size, CancellationToken cancellationToken = default) =>
        GetAsync<ProductResponse>($"products?includeDeleted=true&page={page}&size={size}{(updatedSince is null ? string.Empty : "&updatedSince=" + Format(updatedSince.Value))}", cancellationToken);

    //--------------------------------------------------------------------------------
    // Inventory
    //--------------------------------------------------------------------------------

    public ValueTask<ApiResult<InventoryLevelResponse>> GetInventoryAsync(Guid storeId, DateTime? updatedSince, int page, int size, CancellationToken cancellationToken = default) =>
        GetAsync<InventoryLevelResponse>($"inventory?storeId={storeId}&page={page}&size={size}{(updatedSince is null ? string.Empty : "&updatedSince=" + Format(updatedSince.Value))}", cancellationToken);

    public ValueTask<ApiResult<ProductInventoryResponse>> GetProductInventoryAsync(Guid productId, CancellationToken cancellationToken = default) =>
        GetAsync<ProductInventoryResponse>($"inventory/{productId}", cancellationToken);

    public ValueTask<ApiResult<InventoryChangeResultResponse>> PostInventoryChangesAsync(InventoryChangeRequest request, CancellationToken cancellationToken = default) =>
        PostAsync<InventoryChangeResultResponse>("inventory/changes", request, cancellationToken);

    //--------------------------------------------------------------------------------
    // Customer (オンライン限定)
    //--------------------------------------------------------------------------------

    public ValueTask<ApiResult<CustomerResponseItem>> LookupCustomerAsync(string code, CancellationToken cancellationToken = default) =>
        GetAsync<CustomerResponseItem>($"customers/lookup?code={Uri.EscapeDataString(code)}", cancellationToken);

    public ValueTask<ApiResult<CustomerResponseItem>> GetCustomerAsync(Guid id, CancellationToken cancellationToken = default) =>
        GetAsync<CustomerResponseItem>($"customers/{id}", cancellationToken);

    public ValueTask<ApiResult<CustomerResponse>> SearchCustomersAsync(string keyword, CancellationToken cancellationToken = default) =>
        GetAsync<CustomerResponse>($"customers?keyword={Uri.EscapeDataString(keyword)}&size=50", cancellationToken);

    public ValueTask<ApiResult<PointHistoryResponse>> GetCustomerPointHistoryAsync(Guid id, CancellationToken cancellationToken = default) =>
        GetAsync<PointHistoryResponse>($"customers/{id}/points/history?size=50", cancellationToken);

    public ValueTask<ApiResult<TransactionResponse>> GetCustomerTransactionsAsync(Guid id, CancellationToken cancellationToken = default) =>
        GetAsync<TransactionResponse>($"customers/{id}/transactions?size=50", cancellationToken);

    public ValueTask<ApiResult<CustomerResponseItem>> PostCustomerAsync(CustomerCreateRequest request, CancellationToken cancellationToken = default) =>
        PostAsync<CustomerResponseItem>("customers", request, cancellationToken);

    public ValueTask<ApiResult<CustomerResponseItem>> PutCustomerAsync(Guid id, CustomerUpdateRequest request, CancellationToken cancellationToken = default) =>
        SendAsync<CustomerResponseItem>(HttpMethod.Put, $"customers/{id}", request, cancellationToken);

    //--------------------------------------------------------------------------------
    // Transaction
    //--------------------------------------------------------------------------------

    public ValueTask<ApiResult<TransactionResponseItem>> PostTransactionAsync(TransactionRequest request, CancellationToken cancellationToken = default) =>
        PostAsync<TransactionResponseItem>("transactions", request, cancellationToken);

    public ValueTask<ApiResult<TransactionResponseItem>> PostTransactionVoidAsync(Guid id, TransactionVoidRequest request, CancellationToken cancellationToken = default) =>
        PostAsync<TransactionResponseItem>($"transactions/{id}/void", request, cancellationToken);

    public ValueTask<ApiResult<TransactionResponseItem>> LookupTransactionAsync(string receiptNo, CancellationToken cancellationToken = default) =>
        GetAsync<TransactionResponseItem>($"transactions/lookup?receiptNo={Uri.EscapeDataString(receiptNo)}", cancellationToken);

    public ValueTask<ApiResult<TransactionResponseItem>> GetTransactionAsync(Guid id, CancellationToken cancellationToken = default) =>
        GetAsync<TransactionResponseItem>($"transactions/{id}", cancellationToken);

    //--------------------------------------------------------------------------------
    // Shift
    //--------------------------------------------------------------------------------

    public ValueTask<ApiResult<ShiftResponseItem>> PostShiftAsync(ShiftOpenRequest request, CancellationToken cancellationToken = default) =>
        PostAsync<ShiftResponseItem>("shifts", request, cancellationToken);

    public ValueTask<ApiResult<ShiftResponseItem>> GetCurrentShiftAsync(Guid terminalId, CancellationToken cancellationToken = default) =>
        GetAsync<ShiftResponseItem>($"shifts/current?terminalId={terminalId}", cancellationToken);

    public ValueTask<ApiResult<CashEventResponseItem>> PostCashEventAsync(Guid shiftId, CashEventRequest request, CancellationToken cancellationToken = default) =>
        PostAsync<CashEventResponseItem>($"shifts/{shiftId}/cash-events", request, cancellationToken);

    public ValueTask<ApiResult<ShiftResponseItem>> PostShiftCloseAsync(Guid shiftId, ShiftCloseRequest request, CancellationToken cancellationToken = default) =>
        PostAsync<ShiftResponseItem>($"shifts/{shiftId}/close", request, cancellationToken);

    public ValueTask<ApiResult<ShiftSummaryResponse>> GetShiftSummaryAsync(Guid shiftId, CancellationToken cancellationToken = default) =>
        GetAsync<ShiftSummaryResponse>($"shifts/{shiftId}/summary", cancellationToken);

    //--------------------------------------------------------------------------------
    // Report
    //--------------------------------------------------------------------------------

    public ValueTask<ApiResult<SalesSummaryResponse>> GetSalesSummaryAsync(Guid? storeId, DateOnly from, DateOnly to, string groupBy, CancellationToken cancellationToken = default) =>
        GetAsync<SalesSummaryResponse>($"reports/sales/summary?from={DateTimeHelper.ToIsoDate(from)}&to={DateTimeHelper.ToIsoDate(to)}&groupBy={groupBy}{(storeId is null ? string.Empty : "&storeId=" + storeId)}", cancellationToken);

    //--------------------------------------------------------------------------------
    // Core
    //--------------------------------------------------------------------------------

    private static string Format(DateTime value) => DateTimeHelper.ToIsoDateTime(value);

    private ValueTask<ApiResult<T>> GetAsync<T>(string path, CancellationToken cancellationToken) =>
        SendAsync<T>(HttpMethod.Get, path, null, cancellationToken);

    private ValueTask<ApiResult<T>> PostAsync<T>(string path, object request, CancellationToken cancellationToken) =>
        SendAsync<T>(HttpMethod.Post, path, request, cancellationToken);

#pragma warning disable CA1031
    private async ValueTask<ApiResult<T>> SendAsync<T>(HttpMethod method, string path, object? request, CancellationToken cancellationToken)
    {
        try
        {
            var client = httpClientFactory.CreateClient(ApiNames.Default);
            using var message = new HttpRequestMessage(method, Prefix + path);
            if (request is not null)
            {
                message.Content = JsonContent.Create(request, request.GetType(), options: JsonOptions);
            }

            using var response = await client.SendAsync(message, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
            if (response.IsSuccessStatusCode)
            {
                var content = response.Content.Headers.ContentLength == 0
                    ? default
                    : await response.Content.ReadFromJsonAsync<T>(JsonOptions, cancellationToken).ConfigureAwait(false);
                return new ApiResult<T>(ApiStatus.Success, response.StatusCode, content, null, null);
            }

            var problem = default(ProblemResponse);
            if (response.Content.Headers.ContentType?.MediaType?.Contains("json", StringComparison.OrdinalIgnoreCase) ?? false)
            {
                try
                {
                    problem = await response.Content.ReadFromJsonAsync<ProblemResponse>(JsonOptions, cancellationToken).ConfigureAwait(false);
                }
                catch (JsonException)
                {
                    problem = null;
                }
            }

            return new ApiResult<T>(ApiStatus.HttpError, response.StatusCode, default, problem, null);
        }
        catch (OperationCanceledException ex) when (cancellationToken.IsCancellationRequested)
        {
            return new ApiResult<T>(ApiStatus.Canceled, 0, default, null, ex);
        }
        catch (Exception ex)
        {
            // 接続不可・タイムアウト・DNS 失敗など
            return new ApiResult<T>(ApiStatus.Unavailable, 0, default, null, ex);
        }
    }
#pragma warning restore CA1031
}
