namespace Pos.Terminal.Services;

using System.Text.Json;

using Pos.Shared.Transactions;
using Pos.Terminal.Models.Entity;

using Smart.Data;

// 取引の確定: ローカル取引 + Outbox + 自店在庫キャッシュの増減を 1 トランザクションで書く (販売・返品・取消)
public static class TransactionWriter
{
    public static async ValueTask SaveAsync(IDbProvider provider, DataAccessor accessor, TransactionRequest request, TransactionResponse response)
    {
        ArgumentNullException.ThrowIfNull(provider);
        ArgumentNullException.ThrowIfNull(accessor);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(response);

        var now = DateTime.UtcNow;
        var sign = request.Type == TransactionType.Return ? 1m : -1m;
        var deltas = await ResolveInventoryDeltasAsync(accessor, response.Lines.Select(x => (x.ProductId, x.Quantity * sign)));

        await provider.UsingTxAsync(async (_, tx) =>
        {
            await accessor.InsertTransactionAsync(tx, ToEntity(response));
            await accessor.InsertOutboxAsync(tx, SyncWorker.CreateEntry(OutboxKind.Transaction, request.Id, request, now));
            foreach (var (productId, delta) in deltas)
            {
                await accessor.AddInventoryQuantityAsync(tx, request.StoreId, productId, delta, now);
            }

            await tx.CommitAsync();
        });
    }

    // 取消: ローカル取引の状態を更新し、在庫を戻す
    public static async ValueTask VoidAsync(IDbProvider provider, DataAccessor accessor, TransactionResponse transaction, TransactionVoidRequest request)
    {
        ArgumentNullException.ThrowIfNull(provider);
        ArgumentNullException.ThrowIfNull(accessor);
        ArgumentNullException.ThrowIfNull(transaction);
        ArgumentNullException.ThrowIfNull(request);

        var now = DateTime.UtcNow;
        var sign = transaction.Type == TransactionType.Return ? -1m : 1m;
        var deltas = await ResolveInventoryDeltasAsync(accessor, transaction.Lines.Select(x => (x.ProductId, x.Quantity * sign)));

        transaction.Status = TransactionStatus.Voided;
        transaction.Void = new TransactionResponseVoid { VoidedAt = request.VoidedAt, VoidedByStaffId = request.StaffId, Reason = request.Reason };

        await provider.UsingTxAsync(async (_, tx) =>
        {
            await accessor.UpdateTransactionStatusAsync(tx, transaction.Id, TransactionStatus.Voided, JsonSerializer.Serialize(transaction, HttpService.JsonOptions));
            await accessor.InsertOutboxAsync(tx, SyncWorker.CreateEntry(OutboxKind.TransactionVoid, transaction.Id, request, now));
            foreach (var (productId, delta) in deltas)
            {
                await accessor.AddInventoryQuantityAsync(tx, transaction.StoreId, productId, delta, now);
            }

            await tx.CommitAsync();
        });
    }

    public static LocalTransactionEntity ToEntity(TransactionResponse response)
    {
        ArgumentNullException.ThrowIfNull(response);

        return new LocalTransactionEntity
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
            Payload = JsonSerializer.Serialize(response, HttpService.JsonOptions)
        };
    }

    // 在庫管理対象の商品だけ増減する
    private static async ValueTask<List<(Guid ProductId, decimal Delta)>> ResolveInventoryDeltasAsync(DataAccessor accessor, IEnumerable<(Guid ProductId, decimal Delta)> lines)
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
