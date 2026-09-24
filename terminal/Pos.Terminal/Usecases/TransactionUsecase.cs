namespace Pos.Terminal.Usecases;

using System.Text.Json;

using Pos.Contract.Transactions;
using Pos.Terminal.Models.Entity;

using Smart.Data;

public sealed record TransactionSummary(TransactionResponseItem Detail, OutboxStatus SyncStatus);

// 取引の保存 (ローカル取引 + Outbox + 自店在庫キャッシュを 1 トランザクションで書く)、取消、履歴
public sealed class TransactionUsecase
{
    private readonly IDbProvider provider;

    private readonly DataAccessor accessor;

    private readonly NetworkService network;

    private readonly SyncService sync;

    public TransactionUsecase(
        IDbProvider provider,
        DataAccessor accessor,
        NetworkService network,
        SyncService sync)
    {
        this.provider = provider;
        this.accessor = accessor;
        this.network = network;
        this.sync = sync;
    }

    //--------------------------------------------------------------------------------
    // Query
    //--------------------------------------------------------------------------------

    public async ValueTask<TransactionResponseItem?> QueryAsync(Guid id)
    {
        var entity = await accessor.QueryTransactionAsync(id);
        return entity is null ? null : Deserialize(entity.Payload);
    }

    // ローカルになければ、オンラインのときにサーバから取る (シリアル番号で探した他の端末の取引を開くとき)
    public async ValueTask<TransactionResponseItem?> FindAsync(Guid id)
    {
        var local = await QueryAsync(id);
        if ((local is not null) || !network.IsConnected)
        {
            return local;
        }

        return (await network.ExecuteAsync(h => h.GetTransactionAsync(id), notify: false)).Content;
    }

    public async ValueTask<TransactionResponseItem?> QueryByReceiptNoAsync(string receiptNo)
    {
        var entity = await accessor.QueryTransactionByReceiptNoAsync(receiptNo);
        return entity is null ? null : Deserialize(entity.Payload);
    }

    // 送信状態と内容 (担当・明細・支払を一覧で見せるため) 付きの一覧 (voidedOnly は取消済みだけ)
    public async ValueTask<List<TransactionSummary>> QueryListAsync(Guid? shiftId, DateOnly? businessDate, TransactionType? type, bool voidedOnly, int limit)
    {
        var list = await accessor.QueryTransactionListAsync(shiftId, businessDate, type, limit);
        if (voidedOnly)
        {
            list = list.Where(static x => x.Status == TransactionStatus.Voided).ToList();
        }

        var outbox = await QueryOutboxStatusAsync();
        var result = new List<TransactionSummary>(list.Count);
        foreach (var entity in list)
        {
            var detail = Deserialize(entity.Payload);
            if (detail is not null)
            {
                result.Add(new TransactionSummary(detail, outbox.GetValueOrDefault(entity.Id, OutboxStatus.Sent)));
            }
        }

        return result;
    }

    public async ValueTask<OutboxStatus> QuerySyncStatusAsync(Guid transactionId) =>
        (await QueryOutboxStatusAsync()).GetValueOrDefault(transactionId, OutboxStatus.Sent);

    // 未送信 (Pending / Failed) の取引。要確認が 1 つでもあれば Failed
    private async ValueTask<Dictionary<Guid, OutboxStatus>> QueryOutboxStatusAsync() =>
        (await accessor.QueryOutboxListAsync(null, 1000))
            .Where(static x => x.Kind is OutboxKind.Transaction or OutboxKind.TransactionVoid)
            .GroupBy(static x => x.TargetId)
            .ToDictionary(static g => g.Key, static g => g.Any(static x => x.Status == OutboxStatus.Failed) ? OutboxStatus.Failed : OutboxStatus.Pending);

    //--------------------------------------------------------------------------------
    // Register / Void
    //--------------------------------------------------------------------------------

    // 販売・返品の確定。書いたら送信を促す
    public async ValueTask RegisterAsync(TransactionCreateRequest request, TransactionResponseItem response)
    {
        var now = DateTime.UtcNow;
        var sign = request.Type == TransactionType.Return ? 1m : -1m;
        var deltas = await ResolveInventoryDeltasAsync(response.Lines.Select(x => (x.ProductId, x.Quantity * sign)));

        await provider.UsingTxAsync(async (_, tx) =>
        {
            await accessor.InsertTransactionAsync(tx, ToEntity(response));
            await accessor.InsertOutboxAsync(tx, SyncService.CreateEntry(OutboxKind.Transaction, request.Id, request, now));
            foreach (var (productId, delta) in deltas)
            {
                await accessor.AddInventoryQuantityAsync(tx, request.StoreId, productId, delta, now);
            }

            await tx.CommitAsync();
        });

        await sync.UpdateCountsAsync();
        sync.Trigger();
    }

    // 取消: ローカル取引の状態を更新し、在庫を戻す
    public async ValueTask VoidAsync(TransactionResponseItem transaction, Guid staffId, string reason)
    {
        var now = DateTime.UtcNow;
        var request = new TransactionVoidRequest { StaffId = staffId, Reason = reason, VoidedAt = now };
        var sign = transaction.Type == TransactionType.Return ? -1m : 1m;
        var deltas = await ResolveInventoryDeltasAsync(transaction.Lines.Select(x => (x.ProductId, x.Quantity * sign)));

        transaction.Status = TransactionStatus.Voided;
        transaction.Void = new TransactionResponseVoid { VoidedAt = request.VoidedAt, VoidedByStaffId = request.StaffId, Reason = request.Reason };

        await provider.UsingTxAsync(async (_, tx) =>
        {
            await accessor.UpdateTransactionStatusAsync(tx, transaction.Id, TransactionStatus.Voided, Serialize(transaction));
            await accessor.InsertOutboxAsync(tx, SyncService.CreateEntry(OutboxKind.TransactionVoid, transaction.Id, request, now));
            foreach (var (productId, delta) in deltas)
            {
                await accessor.AddInventoryQuantityAsync(tx, transaction.StoreId, productId, delta, now);
            }

            await tx.CommitAsync();
        });

        await sync.UpdateCountsAsync();
        sync.Trigger();
    }

    //--------------------------------------------------------------------------------
    // Helper
    //--------------------------------------------------------------------------------

    private static string Serialize(TransactionResponseItem transaction) =>
        JsonSerializer.Serialize(transaction, HttpService.JsonOptions);

    private static TransactionResponseItem? Deserialize(string payload) =>
        JsonSerializer.Deserialize<TransactionResponseItem>(payload, HttpService.JsonOptions);

    private static LocalTransactionEntity ToEntity(TransactionResponseItem response) => new()
    {
        Id = response.Id,
        Type = response.Type,
        Status = response.Status,
        ShiftId = response.ShiftId,
        ReceiptNo = response.ReceiptNo,
        BusinessDate = response.BusinessDate,
        TransactedAt = response.TransactedAt,
        CustomerId = response.CustomerId,
        Total = response.Total,
        PointsEarned = response.PointsEarned,
        PointsRedeemed = response.PointsRedeemed,
        OriginalTransactionId = response.OriginalTransactionId,
        Payload = Serialize(response)
    };

    // 在庫管理対象の商品だけ増減する
    private async ValueTask<List<(Guid ProductId, decimal Delta)>> ResolveInventoryDeltasAsync(IEnumerable<(Guid ProductId, decimal Delta)> lines)
    {
        var result = new List<(Guid, decimal)>();
        foreach (var group in lines.GroupBy(static x => x.ProductId))
        {
            var product = await accessor.QueryProductAsync(group.Key);
            if (product is { TrackInventory: true })
            {
                result.Add((group.Key, group.Sum(static x => x.Delta)));
            }
        }

        return result;
    }
}
