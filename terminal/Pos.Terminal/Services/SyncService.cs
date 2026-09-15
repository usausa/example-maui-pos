namespace Pos.Terminal.Services;

using System.Text.Json;

using Pos.Contract.Inventory;
using Pos.Contract.Shifts;
using Pos.Contract.Sync;
using Pos.Contract.Transactions;
using Pos.Terminal.Models.Entity;

using Smart.Data;

// マスタ差分同期と Outbox 送信。バックグラウンドで定期実行し、書き込み直後は Trigger で早める
public sealed class SyncService : IDisposable
{
    private const string ServerTimeKey = "ServerTime";
    private const string InventorySyncKey = "InventorySyncAt";
    private const string ReceiptSeqKey = "ReceiptSeq";

    private const int PageSize = 1000;

    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan MasterSyncInterval = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan MaxBackoff = TimeSpan.FromMinutes(5);

    private readonly ILogger<SyncService> log;

    private readonly IDbProvider provider;

    private readonly DataAccessor accessor;

    private readonly HttpService httpService;

    private readonly Settings settings;

    private readonly Session session;

    private readonly DeviceState deviceState;

    private readonly SemaphoreSlim gate = new(1, 1);

    private readonly SemaphoreSlim wake = new(0);

    private readonly CancellationTokenSource cancellation = new();

    private DateTime nextAttempt = DateTime.MinValue;

    private int failedRounds;

    private DateTime lastMasterSync = DateTime.MinValue;

    public SyncService(
        ILogger<SyncService> log,
        IDbProvider provider,
        DataAccessor accessor,
        HttpService httpService,
        Settings settings,
        Session session,
        DeviceState deviceState)
    {
        this.log = log;
        this.provider = provider;
        this.accessor = accessor;
        this.httpService = httpService;
        this.settings = settings;
        this.session = session;
        this.deviceState = deviceState;
    }

    public void Dispose()
    {
        cancellation.Cancel();
        cancellation.Dispose();
        gate.Dispose();
        wake.Dispose();
    }

    //--------------------------------------------------------------------------------
    // Background
    //--------------------------------------------------------------------------------

    public void Start()
    {
        _ = Task.Run(LoopAsync);
    }

    // 書き込み直後・復帰時に呼ぶ
    public void Trigger()
    {
        nextAttempt = DateTime.MinValue;
        if (wake.CurrentCount == 0)
        {
            wake.Release();
        }
    }

#pragma warning disable CA1031
    private async Task LoopAsync()
    {
        var token = cancellation.Token;
        while (!token.IsCancellationRequested)
        {
            try
            {
                await Task.WhenAny(Task.Delay(Interval, token), wake.WaitAsync(token));
                if (token.IsCancellationRequested || !settings.IsConfigured || !deviceState.NetworkState.IsConnected() || (DateTime.UtcNow < nextAttempt))
                {
                    continue;
                }

                await SendOutboxAsync(token);

                if (DateTime.UtcNow - lastMasterSync > MasterSyncInterval)
                {
                    await SyncMastersAsync(false, null, token);
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                log.ErrorSyncLoop(ex);
            }
        }
    }
#pragma warning restore CA1031

    //--------------------------------------------------------------------------------
    // Master
    //--------------------------------------------------------------------------------

    // 差分同期 (full = true で全件)。マスタは削除 → 挿入で置き換え、自店在庫は updatedSince で取り込む
    public async ValueTask<ApiResult<SyncMastersResponse>> SyncMastersAsync(bool full, IProgress<string>? progress, CancellationToken cancellationToken)
    {
        var storeId = settings.StoreId;
        if (storeId is null)
        {
            return new ApiResult<SyncMastersResponse>(ApiStatus.Unavailable, 0, null, null, null);
        }

        await gate.WaitAsync(cancellationToken);
        try
        {
            progress?.Report("マスタを取得しています...");
            var since = full ? null : await QueryDateAsync(ServerTimeKey);
            var result = await httpService.GetSyncMastersAsync(since, cancellationToken);
            if (!result.IsSuccess)
            {
                return result;
            }

            var response = result.Content!;
            progress?.Report("マスタを保存しています...");
            await provider.UsingTxAsync(async (_, tx) =>
            {
                if (response.Settings is not null)
                {
                    await accessor.DeleteSettingsAsync(tx);
                    await accessor.InsertSettingsAsync(tx, response.Settings);
                }

                foreach (var x in response.Stores)
                {
                    await accessor.DeleteStoreAsync(tx, x.Id);
                    await accessor.InsertStoreAsync(tx, x);
                }

                foreach (var x in response.Terminals)
                {
                    await accessor.DeleteTerminalAsync(tx, x.Id);
                    await accessor.InsertTerminalAsync(tx, x);
                }

                foreach (var x in response.Staff)
                {
                    await accessor.DeleteStaffAsync(tx, x.Id);
                    await accessor.InsertStaffAsync(tx, x);
                }

                foreach (var x in response.Categories)
                {
                    await accessor.DeleteCategoryAsync(tx, x.Id);
                    await accessor.InsertCategoryAsync(tx, x);
                }

                foreach (var x in response.TaxRates)
                {
                    await accessor.DeleteTaxRateAsync(tx, x.Id);
                    await accessor.InsertTaxRateAsync(tx, x);
                }

                foreach (var x in response.Products)
                {
                    await accessor.DeleteProductAsync(tx, x.Id);
                    await accessor.InsertProductAsync(tx, x);
                }

                foreach (var x in response.Discounts)
                {
                    await accessor.DeleteDiscountAsync(tx, x.Id);
                    await accessor.InsertDiscountAsync(tx, x);
                }

                foreach (var x in response.PaymentMethods)
                {
                    await accessor.DeletePaymentMethodAsync(tx, x.Id);
                    await accessor.InsertPaymentMethodAsync(tx, x);
                }

                foreach (var x in response.AdjustmentReasons)
                {
                    await accessor.DeleteAdjustmentReasonAsync(tx, x.Id);
                    await accessor.InsertAdjustmentReasonAsync(tx, x);
                }

                await tx.CommitAsync(cancellationToken);
            }, cancellationToken);

            // 商品が多くて省かれたときはページで取り込む
            if (response.ProductsTruncated)
            {
                progress?.Report("商品を取得しています...");
                for (var page = 0; ; page++)
                {
                    var products = await httpService.GetProductsAsync(since, page, PageSize, cancellationToken);
                    if (!products.IsSuccess)
                    {
                        return new ApiResult<SyncMastersResponse>(products.Status, products.StatusCode, null, products.Problem, products.Exception);
                    }

                    var items = products.Content!.Items;
                    await provider.UsingTxAsync(async (_, tx) =>
                    {
                        foreach (var x in items)
                        {
                            await accessor.DeleteProductAsync(tx, x.Id);
                            await accessor.InsertProductAsync(tx, x);
                        }

                        await tx.CommitAsync(cancellationToken);
                    }, cancellationToken);

                    if (items.Count < PageSize)
                    {
                        break;
                    }
                }
            }

            // 自店在庫
            progress?.Report("在庫を取得しています...");
            var inventorySince = full ? null : await QueryDateAsync(InventorySyncKey);
            for (var page = 0; ; page++)
            {
                var inventory = await httpService.GetInventoryAsync(storeId.Value, inventorySince, page, PageSize, cancellationToken);
                if (!inventory.IsSuccess)
                {
                    return new ApiResult<SyncMastersResponse>(inventory.Status, inventory.StatusCode, null, inventory.Problem, inventory.Exception);
                }

                var items = inventory.Content!.Items;
                await provider.UsingTxAsync(async (_, tx) =>
                {
                    foreach (var x in items)
                    {
                        await accessor.UpsertInventoryLevelAsync(tx, x.StoreId, x.ProductId, x.Quantity, x.UpdatedAt);
                    }

                    await tx.CommitAsync(cancellationToken);
                }, cancellationToken);

                if (items.Count < PageSize)
                {
                    break;
                }
            }

            await accessor.UpsertSyncStateAsync(ServerTimeKey, DateTimeHelper.ToRoundTrip(response.ServerTime));
            await accessor.UpsertSyncStateAsync(InventorySyncKey, DateTimeHelper.ToRoundTrip(response.ServerTime));
            lastMasterSync = DateTime.UtcNow;

            await RefreshSessionAsync();
            await MainThread.InvokeOnMainThreadAsync(() => session.LastSyncAt = DateTime.UtcNow);
            log.InfoMasterSynced(response.Products.Count, response.ServerTime);
            return result;
        }
        finally
        {
            gate.Release();
        }
    }

    // 手動同期: マスタを取り込み、成功したら未送信も送る (sent は送信件数)
    public async ValueTask<(ApiResult<SyncMastersResponse> Result, int Sent)> SyncAllAsync(IProgress<string>? progress, CancellationToken cancellationToken)
    {
        var result = await SyncMastersAsync(false, progress, cancellationToken);
        var sent = result.IsSuccess ? await SendOutboxAsync(cancellationToken) : 0;
        return (result, sent);
    }

    // ローカル DB の会社設定・店舗・端末を Session に反映する
    public async ValueTask RefreshSessionAsync()
    {
        var companySettings = await accessor.QuerySettingsAsync();
        var store = settings.StoreId is null ? null : await accessor.QueryStoreAsync(settings.StoreId.Value);
        var terminal = settings.TerminalId is null ? null : await accessor.QueryTerminalAsync(settings.TerminalId.Value);
        var shift = settings.TerminalId is null ? null : await accessor.QueryCurrentShiftAsync(settings.TerminalId.Value);
        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            session.CompanySettings = companySettings;
            session.Store = store;
            session.Terminal = terminal;
            session.CurrentShift = shift;
        });
        await UpdateCountsAsync();
    }

    private async ValueTask<DateTime?> QueryDateAsync(string key)
    {
        var value = await accessor.QuerySyncStateAsync(key);
        return DateTimeHelper.TryParseRoundTrip(value, out var date) ? date : null;
    }

    //--------------------------------------------------------------------------------
    // Receipt No
    //--------------------------------------------------------------------------------

    // {店舗コード}-{端末番号:00}-{連番:000000}。連番はローカルで採番し、サーバの LastReceiptSeq より小さければ合わせる
    public async ValueTask<string> NextReceiptNoAsync()
    {
        var value = await accessor.QuerySyncStateAsync(ReceiptSeqKey);
        var seq = Int32.TryParse(value, CultureInfo.InvariantCulture, out var last) ? last : 0;
        seq = Math.Max(seq, session.Terminal?.LastReceiptSeq ?? 0) + 1;
        await accessor.UpsertSyncStateAsync(ReceiptSeqKey, seq.ToString(CultureInfo.InvariantCulture));
        return $"{session.Store?.Code}-{session.Terminal?.TerminalNo ?? 0:00}-{seq:000000}";
    }

    //--------------------------------------------------------------------------------
    // Outbox
    //--------------------------------------------------------------------------------

    public static OutboxEntity CreateEntry(OutboxKind kind, Guid targetId, object payload, DateTime now) => new()
    {
        Id = Guid.NewGuid(),
        Kind = kind,
        TargetId = targetId,
        Payload = JsonSerializer.Serialize(payload, payload.GetType(), HttpService.JsonOptions),
        CreatedAt = now,
        Status = OutboxStatus.Pending
    };

    public async ValueTask UpdateCountsAsync()
    {
        var pending = await accessor.CountOutboxAsync(OutboxStatus.Pending);
        var failed = await accessor.CountOutboxAsync(OutboxStatus.Failed);
        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            session.UnsentCount = (int)(pending + failed);
            session.FailedCount = (int)failed;
        });
    }

    // 発生順に送信する。409 / 422 は要確認 (Failed) として止め、後続は送らない。5xx / 通信エラーは次回に持ち越す
    public async ValueTask<int> SendOutboxAsync(CancellationToken cancellationToken)
    {
        if (!deviceState.NetworkState.IsConnected())
        {
            return 0;
        }

        await gate.WaitAsync(cancellationToken);
        try
        {
            var sent = 0;
            var items = await accessor.QueryOutboxListAsync(null, 100);
            foreach (var item in items)
            {
                if (item.Status == OutboxStatus.Failed)
                {
                    break;
                }

                var result = await SendAsync(item, cancellationToken);
                if (result.IsSuccess)
                {
                    await accessor.UpdateOutboxAsync(item.Id, OutboxStatus.Sent, item.Attempts + 1, null, DateTime.UtcNow);
                    sent++;
                    continue;
                }

                if (result.IsRejected)
                {
                    log.WarnOutboxRejected(item.Kind, item.TargetId, (int)result.StatusCode, result.ErrorCode);
                    await accessor.UpdateOutboxAsync(item.Id, OutboxStatus.Failed, item.Attempts + 1, $"{result.ErrorCode ?? ((int)result.StatusCode).ToString(CultureInfo.InvariantCulture)}: {result.Message}", null);
                    break;
                }

                // 一時的な失敗: 指数バックオフ
                await accessor.UpdateOutboxAsync(item.Id, OutboxStatus.Pending, item.Attempts + 1, result.Message, null);
                failedRounds = Math.Min(failedRounds + 1, 6);
                nextAttempt = DateTime.UtcNow + TimeSpan.FromSeconds(Math.Min(Math.Pow(2, failedRounds) * 5, MaxBackoff.TotalSeconds));
                await UpdateCountsAsync();
                return sent;
            }

            failedRounds = 0;
            await accessor.DeleteSentOutboxAsync(DateTime.UtcNow.AddDays(-7));
            await UpdateCountsAsync();
            return sent;
        }
        finally
        {
            gate.Release();
        }
    }

    // 要確認を再送対象に戻す
    public async ValueTask RetryAsync(Guid id)
    {
        var item = await accessor.QueryOutboxAsync(id);
        if (item is not null)
        {
            await accessor.UpdateOutboxAsync(id, OutboxStatus.Pending, item.Attempts, item.LastError, null);
        }

        await UpdateCountsAsync();
        Trigger();
    }

    // 要確認を破棄する (サーバには送らない)
    public async ValueTask DiscardAsync(Guid id)
    {
        await accessor.DeleteOutboxAsync(id);
        await UpdateCountsAsync();
    }

    private async ValueTask<ApiResult<object>> SendAsync(OutboxEntity item, CancellationToken cancellationToken)
    {
        switch (item.Kind)
        {
            case OutboxKind.ShiftOpen:
            {
                var result = await httpService.PostShiftAsync(Deserialize<ShiftOpenRequest>(item.Payload), cancellationToken);
                return ToPlain(result);
            }

            case OutboxKind.Transaction:
            {
                var result = await httpService.PostTransactionAsync(Deserialize<TransactionCreateRequest>(item.Payload), cancellationToken);
                if (result.IsSuccess && (result.Content is not null))
                {
                    await accessor.UpdateTransactionAsync(item.TargetId, result.Content.Status, JsonSerializer.Serialize(result.Content, HttpService.JsonOptions));
                }

                return ToPlain(result);
            }

            case OutboxKind.TransactionVoid:
            {
                var result = await httpService.PostTransactionVoidAsync(item.TargetId, Deserialize<TransactionVoidRequest>(item.Payload), cancellationToken);
                if (result.IsSuccess && (result.Content is not null))
                {
                    await accessor.UpdateTransactionAsync(item.TargetId, result.Content.Status, JsonSerializer.Serialize(result.Content, HttpService.JsonOptions));
                }

                return ToPlain(result);
            }

            case OutboxKind.CashEvent:
                return ToPlain(await httpService.PostCashEventAsync(item.TargetId, Deserialize<ShiftCashEventRequest>(item.Payload), cancellationToken));

            case OutboxKind.ShiftClose:
                return ToPlain(await httpService.PostShiftCloseAsync(item.TargetId, Deserialize<ShiftCloseRequest>(item.Payload), cancellationToken));

            case OutboxKind.InventoryChanges:
                return ToPlain(await httpService.PostInventoryChangesAsync(Deserialize<InventoryChangeRequest>(item.Payload), cancellationToken));

            default:
                throw new InvalidOperationException($"Unsupported outbox kind. kind=[{item.Kind}]");
        }
    }

    private static T Deserialize<T>(string json) =>
        JsonSerializer.Deserialize<T>(json, HttpService.JsonOptions) ?? throw new InvalidOperationException("Invalid payload.");

    private static ApiResult<object> ToPlain<T>(ApiResult<T> result) =>
        new(result.Status, result.StatusCode, result.Content, result.Problem, result.Exception);
}
