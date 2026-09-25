namespace Pos.Terminal.Services;

using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Unicode;

using Pos.Contract.Customers;
using Pos.Contract.Inventory;
using Pos.Contract.InventoryReceipts;
using Pos.Contract.InventoryTransfers;
using Pos.Contract.Orders;
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

    private readonly ApiContext apiContext;

    public static JsonSerializerOptions JsonOptions { get; } = CreateJsonOptions();

    public HttpService(
        IHttpClientFactory httpClientFactory,
        ApiContext apiContext)
    {
        this.httpClientFactory = httpClientFactory;
        this.apiContext = apiContext;
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
    // Terminal
    //--------------------------------------------------------------------------------

    // ペアリング (トークンはまだないので付けない)
    public ValueTask<ApiResult<TerminalPairResponse>> PairAsync(TerminalPairRequest request, CancellationToken cancellationToken = default) =>
        PostAsync<TerminalPairResponse>("terminals/pair", request, cancellationToken);

    public ValueTask<ApiResult<object>> HeartbeatAsync(TerminalHeartbeatRequest request, CancellationToken cancellationToken = default) =>
        PostAsync<object>("terminals/me/heartbeat", request, cancellationToken);

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

    public ValueTask<ApiResult<InventoryProductResponse>> GetProductInventoryAsync(Guid productId, CancellationToken cancellationToken = default) =>
        GetAsync<InventoryProductResponse>($"inventory/{productId}", cancellationToken);

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

    public ValueTask<ApiResult<CustomerPointHistoryResponse>> GetCustomerPointHistoryAsync(Guid id, CancellationToken cancellationToken = default) =>
        GetAsync<CustomerPointHistoryResponse>($"customers/{id}/points/history?size=50", cancellationToken);

    public ValueTask<ApiResult<TransactionResponse>> GetCustomerTransactionsAsync(Guid id, CancellationToken cancellationToken = default) =>
        GetAsync<TransactionResponse>($"customers/{id}/transactions?size=50", cancellationToken);

    public ValueTask<ApiResult<CustomerResponseItem>> PostCustomerAsync(CustomerCreateRequest request, CancellationToken cancellationToken = default) =>
        PostAsync<CustomerResponseItem>("customers", request, cancellationToken);

    public ValueTask<ApiResult<CustomerResponseItem>> PutCustomerAsync(Guid id, CustomerUpdateRequest request, CancellationToken cancellationToken = default) =>
        SendAsync<CustomerResponseItem>(HttpMethod.Put, $"customers/{id}", request, cancellationToken);

    //--------------------------------------------------------------------------------
    // Transaction
    //--------------------------------------------------------------------------------

    public ValueTask<ApiResult<TransactionResponseItem>> PostTransactionAsync(TransactionCreateRequest request, CancellationToken cancellationToken = default) =>
        PostAsync<TransactionResponseItem>("transactions", request, cancellationToken);

    public ValueTask<ApiResult<TransactionResponseItem>> PostTransactionVoidAsync(Guid id, TransactionVoidRequest request, CancellationToken cancellationToken = default) =>
        PostAsync<TransactionResponseItem>($"transactions/{id}/void", request, cancellationToken);

    public ValueTask<ApiResult<TransactionResponseItem>> LookupTransactionAsync(string receiptNo, CancellationToken cancellationToken = default) =>
        GetAsync<TransactionResponseItem>($"transactions/lookup?receiptNo={Uri.EscapeDataString(receiptNo)}", cancellationToken);

    // シリアル番号 (完全一致) を含む取引。新しい順
    public ValueTask<ApiResult<TransactionResponse>> GetTransactionsBySerialAsync(string serialNumber, CancellationToken cancellationToken = default) =>
        GetAsync<TransactionResponse>($"transactions?serialNumber={Uri.EscapeDataString(serialNumber)}&size=50", cancellationToken);

    public ValueTask<ApiResult<TransactionResponseItem>> GetTransactionAsync(Guid id, CancellationToken cancellationToken = default) =>
        GetAsync<TransactionResponseItem>($"transactions/{id}", cancellationToken);

    //--------------------------------------------------------------------------------
    // Order (オンライン限定)
    //--------------------------------------------------------------------------------

    // open = true は未完了 (入荷待ち・引き渡し待ち) だけ。keyword は受注番号・宛名・電話の部分一致
    public ValueTask<ApiResult<OrderResponse>> GetOrdersAsync(Guid storeId, OrderStatus? status, bool open, string? keyword, CancellationToken cancellationToken = default) =>
        GetAsync<OrderResponse>($"orders?storeId={storeId}&size=100{(open ? "&open=true" : string.Empty)}{(status is null ? string.Empty : "&status=" + status)}{(String.IsNullOrEmpty(keyword) ? string.Empty : "&keyword=" + Uri.EscapeDataString(keyword))}", cancellationToken);

    public ValueTask<ApiResult<OrderResponseItem>> GetOrderAsync(Guid id, CancellationToken cancellationToken = default) =>
        GetAsync<OrderResponseItem>($"orders/{id}", cancellationToken);

    public ValueTask<ApiResult<OrderResponseItem>> PostOrderAsync(OrderCreateRequest request, CancellationToken cancellationToken = default) =>
        PostAsync<OrderResponseItem>("orders", request, cancellationToken);

    public ValueTask<ApiResult<OrderResponseItem>> PostOrderArriveAsync(Guid id, CancellationToken cancellationToken = default) =>
        SendAsync<OrderResponseItem>(HttpMethod.Post, $"orders/{id}/arrive", null, cancellationToken);

    public ValueTask<ApiResult<OrderResponseItem>> PostOrderCancelAsync(Guid id, OrderCancelRequest request, CancellationToken cancellationToken = default) =>
        PostAsync<OrderResponseItem>($"orders/{id}/cancel", request, cancellationToken);

    public ValueTask<ApiResult<OrderResponseItem>> PostOrderDepositAsync(Guid id, OrderDepositRequest request, CancellationToken cancellationToken = default) =>
        PostAsync<OrderResponseItem>($"orders/{id}/deposit", request, cancellationToken);

    public ValueTask<ApiResult<OrderResponseItem>> PostOrderDepositRefundAsync(Guid id, OrderDepositRefundRequest request, CancellationToken cancellationToken = default) =>
        PostAsync<OrderResponseItem>($"orders/{id}/deposit/refund", request, cancellationToken);

    //--------------------------------------------------------------------------------
    // Receiving (オンライン限定)
    //--------------------------------------------------------------------------------

    public ValueTask<ApiResult<InventoryReceiptResponse>> GetInventoryReceiptsAsync(Guid storeId, InventoryReceiptStatus status, CancellationToken cancellationToken = default) =>
        GetAsync<InventoryReceiptResponse>($"inventory/receipts?storeId={storeId}&status={status}&size=100", cancellationToken);

    public ValueTask<ApiResult<InventoryReceiptResponseItem>> PostInventoryReceiptReceiveAsync(Guid id, InventoryReceiptReceiveRequest request, CancellationToken cancellationToken = default) =>
        PostAsync<InventoryReceiptResponseItem>($"inventory/receipts/{id}/receive", request, cancellationToken);

    // 自店宛の移動
    public ValueTask<ApiResult<InventoryTransferResponse>> GetInventoryTransfersAsync(Guid toStoreId, InventoryTransferStatus status, CancellationToken cancellationToken = default) =>
        GetAsync<InventoryTransferResponse>($"inventory/transfers?toStoreId={toStoreId}&status={status}&size=100", cancellationToken);

    public ValueTask<ApiResult<InventoryTransferResponseItem>> PostInventoryTransferReceiveAsync(Guid id, InventoryTransferReceiveRequest request, CancellationToken cancellationToken = default) =>
        PostAsync<InventoryTransferResponseItem>($"inventory/transfers/{id}/receive", request, cancellationToken);

    //--------------------------------------------------------------------------------
    // Shift
    //--------------------------------------------------------------------------------

    public ValueTask<ApiResult<ShiftResponseItem>> PostShiftAsync(ShiftOpenRequest request, CancellationToken cancellationToken = default) =>
        PostAsync<ShiftResponseItem>("shifts", request, cancellationToken);

    public ValueTask<ApiResult<ShiftResponseItem>> GetCurrentShiftAsync(Guid terminalId, CancellationToken cancellationToken = default) =>
        GetAsync<ShiftResponseItem>($"shifts/current?terminalId={terminalId}", cancellationToken);

    public ValueTask<ApiResult<ShiftCashEventResponseItem>> PostCashEventAsync(Guid shiftId, ShiftCashEventRequest request, CancellationToken cancellationToken = default) =>
        PostAsync<ShiftCashEventResponseItem>($"shifts/{shiftId}/cash-events", request, cancellationToken);

    public ValueTask<ApiResult<ShiftResponseItem>> PostShiftCloseAsync(Guid shiftId, ShiftCloseRequest request, CancellationToken cancellationToken = default) =>
        PostAsync<ShiftResponseItem>($"shifts/{shiftId}/close", request, cancellationToken);

    public ValueTask<ApiResult<ShiftSummaryResponse>> GetShiftSummaryAsync(Guid shiftId, CancellationToken cancellationToken = default) =>
        GetAsync<ShiftSummaryResponse>($"shifts/{shiftId}/summary", cancellationToken);

    //--------------------------------------------------------------------------------
    // Report
    //--------------------------------------------------------------------------------

    public ValueTask<ApiResult<ReportSalesSummaryResponse>> GetSalesSummaryAsync(Guid? storeId, DateOnly from, DateOnly to, string groupBy, CancellationToken cancellationToken = default) =>
        GetAsync<ReportSalesSummaryResponse>($"reports/sales/summary?from={DateTimeHelper.ToIsoDate(from)}&to={DateTimeHelper.ToIsoDate(to)}&groupBy={groupBy}{(storeId is null ? string.Empty : "&storeId=" + storeId)}", cancellationToken);

    //--------------------------------------------------------------------------------
    // Image
    //--------------------------------------------------------------------------------

    // 画像などのバイト列 (path は接続先からの相対パス)
#pragma warning disable CA1031
    public async ValueTask<ApiResult<byte[]>> GetBytesAsync(Uri path, CancellationToken cancellationToken = default)
    {
        try
        {
            var client = httpClientFactory.CreateClient(ApiNames.Default);
            using var message = new HttpRequestMessage(HttpMethod.Get, path);
            var token = Authorize(message);
            using var response = await client.SendAsync(message, cancellationToken).ConfigureAwait(false);
            NotifyIfUnauthorized(response, token);
            return response.IsSuccessStatusCode
                ? new ApiResult<byte[]>(ApiStatus.Success, response.StatusCode, await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false), null, null)
                : new ApiResult<byte[]>(ApiStatus.HttpError, response.StatusCode, null, null, null);
        }
        catch (OperationCanceledException ex) when (cancellationToken.IsCancellationRequested)
        {
            return new ApiResult<byte[]>(ApiStatus.Canceled, 0, null, null, ex);
        }
        catch (Exception ex)
        {
            return new ApiResult<byte[]>(ApiStatus.Unavailable, 0, null, null, ex);
        }
    }
#pragma warning restore CA1031

    //--------------------------------------------------------------------------------
    // Core
    //--------------------------------------------------------------------------------

    private static string Format(DateTime value) => DateTimeHelper.ToIsoDateTime(value);

    // 端末のトークンを付ける (付けたトークンを返す)
    private string? Authorize(HttpRequestMessage message)
    {
        var token = apiContext.Token;
        if (token is not null)
        {
            message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        return token;
    }

    // 付けたトークンが 401 になったら登録が無効になったと知らせる (未登録の要求の 401 は知らせない)
    private void NotifyIfUnauthorized(HttpResponseMessage response, string? token)
    {
        if ((response.StatusCode == HttpStatusCode.Unauthorized) && (token is not null))
        {
            apiContext.NotifyUnauthorized(token);
        }
    }

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
            var token = Authorize(message);
            if (request is not null)
            {
                message.Content = JsonContent.Create(request, request.GetType(), options: JsonOptions);
            }

            using var response = await client.SendAsync(message, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
            NotifyIfUnauthorized(response, token);
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
