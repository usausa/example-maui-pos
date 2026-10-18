namespace Pos.Terminal.Usecases;

using System.Text.Json;

using Pos.Contract.Transactions;
using Pos.Terminal.Models.Entity;

using Smart.Data;
using Smart.Mapper;

public sealed record TransactionSummary(TransactionListResponseItem Detail, OutboxStatus SyncStatus);

// 取引の保存 (ローカル取引 + Outbox + 自店在庫キャッシュ + 返品の元の取引の返品済みの数量を 1 トランザクションで書く)、取消、履歴
public sealed partial class TransactionUsecase
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

    public async ValueTask<TransactionListResponseItem?> QueryAsync(Guid id)
    {
        var entity = await accessor.QueryTransactionAsync(id);
        return entity is null ? null : Deserialize(entity.Payload);
    }

    // ローカルになければ、オンラインのときにサーバから取る (シリアル番号で探した他の端末の取引を開くとき)
    public async ValueTask<TransactionListResponseItem?> FindAsync(Guid id)
    {
        var local = await QueryAsync(id);
        if ((local is not null) || !network.IsConnected)
        {
            return local;
        }

        return (await network.ExecuteAsync(h => h.GetTransactionAsync(id), notify: false)).Content;
    }

    public async ValueTask<TransactionListResponseItem?> QueryByReceiptNoAsync(string receiptNo)
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

    // 返品のある販売はサーバが取消を拒むので、明細の返品済みの数量 (端末で記録した返品も足してある) と、端末で記録した返品で判定する
    public async ValueTask<bool> HasReturnsAsync(TransactionListResponseItem transaction) =>
        transaction.Lines.Any(static x => x.ReturnedQuantity > 0) || (await accessor.CountReturnsAsync(transaction.Id) > 0);

    // 他の端末の返品はサーバの明細にしかないので、オンラインなら販売の取消の前にサーバの最新で確かめる。
    // その販売の返品 (とその取消) に未送信があるとサーバの状態が端末より古いので確かめず、取れないときも確かめずに進め、送ったときのサーバの判定に任せる
    public async ValueTask<bool> HasServerReturnsAsync(TransactionListResponseItem transaction)
    {
        if ((transaction.Type != TransactionType.Sale) || !network.IsConnected || (await accessor.CountUnsentReturnsAsync(transaction.Id) > 0))
        {
            return false;
        }

        var result = await network.ExecuteAsync(h => h.GetTransactionAsync(transaction.Id), notify: false);
        return result.Content?.Lines.Any(static x => x.ReturnedQuantity > 0) ?? false;
    }

    // サーバから取った元の取引に、この端末でまだ送っていない取消と返品を反映する (サーバの最新にはまだ入っていない)
    public async ValueTask ApplyUnsentAsync(TransactionListResponseItem original)
    {
        // 端末で取り消した取引は、取消を送るまで端末の取消の状態と情報にする (返品させない)
        if ((original.Status == TransactionStatus.Completed) && (await QueryAsync(original.Id) is { Status: TransactionStatus.Voided } local))
        {
            original.Status = TransactionStatus.Voided;
            original.Void = local.Void;
        }

        var outbox = await QueryOutboxStatusAsync();
        foreach (var entity in await accessor.QueryReturnListAsync(original.Id))
        {
            if (outbox.ContainsKey(entity.Id) && (Deserialize(entity.Payload) is { } returned))
            {
                ApplyReturnedQuantity(original, returned.Lines, 1m);
            }
        }
    }

    // 未送信 (Pending / Failed) の取引。要確認が 1 つでもあれば Failed
    private async ValueTask<Dictionary<Guid, OutboxStatus>> QueryOutboxStatusAsync() =>
        (await accessor.QueryOutboxListAsync(1000))
            .Where(static x => x.Kind is OutboxKind.Transaction or OutboxKind.TransactionVoid)
            .GroupBy(static x => x.TargetId)
            .ToDictionary(static g => g.Key, static g => g.Any(static x => x.Status == OutboxStatus.Failed) ? OutboxStatus.Failed : OutboxStatus.Pending);

    //--------------------------------------------------------------------------------
    // Register / Void
    //--------------------------------------------------------------------------------

    // 販売・返品の確定。返品は元の取引 (この端末の取引のとき) の返品済みの数量に足す。書いたら送信を促す
    public async ValueTask RegisterAsync(TransactionCreateRequest request, TransactionListResponseItem response)
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

            if (response.OriginalTransactionId is { } originalId)
            {
                await AddReturnedQuantityAsync(tx, originalId, response.Lines, 1m);
            }

            await tx.CommitAsync();
        });

        await sync.UpdateCountsAsync();
        sync.Trigger();
    }

    // 取消: ローカル取引の状態を更新し、在庫と、返品なら元の取引の返品済みの数量を戻す。approvedByStaffId はレジ係の取消を承認した店長以上
    public async ValueTask VoidAsync(TransactionListResponseItem transaction, Guid staffId, Guid? approvedByStaffId, string reason)
    {
        var now = DateTime.UtcNow;
        var request = new TransactionVoidRequest { StaffId = staffId, ApprovedByStaffId = approvedByStaffId, Reason = reason, VoidedAt = now };
        var sign = transaction.Type == TransactionType.Return ? -1m : 1m;
        var deltas = await ResolveInventoryDeltasAsync(transaction.Lines.Select(x => (x.ProductId, x.Quantity * sign)));

        transaction.Status = TransactionStatus.Voided;
        transaction.Void = new TransactionListResponseVoid { VoidedAt = request.VoidedAt, VoidedByStaffId = request.StaffId, Reason = request.Reason };

        await provider.UsingTxAsync(async (_, tx) =>
        {
            await accessor.UpdateTransactionAsync(tx, transaction.Id, TransactionStatus.Voided, Serialize(transaction));
            await accessor.InsertOutboxAsync(tx, SyncService.CreateEntry(OutboxKind.TransactionVoid, transaction.Id, request, now));
            foreach (var (productId, delta) in deltas)
            {
                await accessor.AddInventoryQuantityAsync(tx, transaction.StoreId, productId, delta, now);
            }

            if (transaction.OriginalTransactionId is { } originalId)
            {
                await AddReturnedQuantityAsync(tx, originalId, transaction.Lines, -1m);
            }

            await tx.CommitAsync();
        });

        await sync.UpdateCountsAsync();
        sync.Trigger();
    }

    //--------------------------------------------------------------------------------
    // Helper
    //--------------------------------------------------------------------------------

    private static string Serialize(TransactionListResponseItem transaction) =>
        JsonSerializer.Serialize(transaction, HttpService.JsonOptions);

    private static TransactionListResponseItem? Deserialize(string payload) =>
        JsonSerializer.Deserialize<TransactionListResponseItem>(payload, HttpService.JsonOptions);

    [Mapper]
    [MapUsing(nameof(LocalTransactionEntity.Payload), nameof(Serialize))]
    private static partial LocalTransactionEntity ToEntity(TransactionListResponseItem response);

    // 元の取引がこの端末の取引なら、返品の明細の数量だけ返品済みの数量を増減する (詳細の返品済みと、オフラインの返品の上限に使う)
    private async ValueTask AddReturnedQuantityAsync(DbTransaction tx, Guid originalId, IEnumerable<TransactionListResponseLine> lines, decimal sign)
    {
        var entity = await accessor.QueryTransactionForUpdateAsync(tx, originalId);
        var original = entity is null ? null : Deserialize(entity.Payload);
        if (original is null)
        {
            return;
        }

        ApplyReturnedQuantity(original, lines, sign);
        await accessor.UpdateTransactionAsync(tx, original.Id, original.Status, Serialize(original));
    }

    // 返品の明細の数量だけ元の明細の返品済みの数量を増減する。前の版で記録した返品は返品済みに足していないので、その取消で負にしない
    private static void ApplyReturnedQuantity(TransactionListResponseItem original, IEnumerable<TransactionListResponseLine> lines, decimal sign)
    {
        foreach (var line in lines)
        {
            if (original.Lines.FirstOrDefault(x => x.Id == line.OriginalLineId) is { } originalLine)
            {
                originalLine.ReturnedQuantity = Math.Max(0m, originalLine.ReturnedQuantity + (line.Quantity * sign));
            }
        }
    }

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
