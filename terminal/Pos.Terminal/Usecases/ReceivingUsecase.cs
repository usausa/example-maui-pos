namespace Pos.Terminal.Usecases;

// 入荷・店舗間移動の受領 (オンライン限定): 自店の受領待ちの伝票を集め、数えた数で受領して在庫の同期を促す
public sealed class ReceivingUsecase
{
    private readonly Session session;

    private readonly NetworkService network;

    private readonly SyncService sync;

    public ReceivingUsecase(
        Session session,
        NetworkService network,
        SyncService sync)
    {
        this.session = session;
        this.network = network;
        this.sync = sync;
    }

    // 入荷予定と、自店宛に出荷済みの移動。取得できなければ null。
    // 2 つの取得は 1 回の実行にまとめ、通信中の表示を 1 回にする (続けて出し入れすると描画が止まることがある)
    public async ValueTask<List<ReceivingDocument>?> QueryPendingAsync()
    {
        if (session.StoreId is not { } storeId)
        {
            return [];
        }

        var result = await network.ExecuteAsync(async h =>
        {
            var receipts = await h.GetInventoryReceiptsAsync(storeId, InventoryReceiptStatus.Draft);
            if (!receipts.IsSuccess)
            {
                return Failed(receipts);
            }

            var transfers = await h.GetInventoryTransfersAsync(storeId, InventoryTransferStatus.Shipped);
            if (!transfers.IsSuccess)
            {
                return Failed(transfers);
            }

            var documents = receipts.Content!.Items.Select(ReceivingMapper.FromReceipt)
                .Concat(transfers.Content!.Items.Select(ReceivingMapper.FromTransfer))
                .ToList();
            return new ApiResult<List<ReceivingDocument>>(ApiStatus.Success, HttpStatusCode.OK, documents, null, null);
        }, notify: false);
        return result.IsSuccess ? result.Content : null;
    }

    // 受領した在庫はサーバが増やすので、端末の在庫は次の差分の同期で追い付く
    public async ValueTask<bool> ReceiveAsync(ReceivingDocument document, IReadOnlyDictionary<Guid, decimal> counts)
    {
        var staffId = session.Staff?.Id;
        var received = document.Kind == ReceivingKind.Receipt
            ? (await network.ExecuteAsync(h => h.PostInventoryReceiptReceiveAsync(document.Id, ReceivingMapper.ToReceiptRequest(staffId, counts)))).IsSuccess
            : (await network.ExecuteAsync(h => h.PostInventoryTransferReceiveAsync(document.Id, ReceivingMapper.ToTransferRequest(staffId, counts)))).IsSuccess;
        if (received)
        {
            sync.TriggerMasterSync();
        }

        return received;
    }

    private static ApiResult<List<ReceivingDocument>> Failed<T>(ApiResult<T> result) =>
        new(result.Status, result.StatusCode, null, result.Problem, result.Exception);
}
