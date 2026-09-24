namespace Pos.Server.Accessors;

using Pos.Server.Models.Entity;

// 店舗間移動 (依頼と明細)。在庫の加減算と変動履歴は InventoryAccessor
[DataAccessor]
[ExecuteConfig(typeof(DataProfile))]
public sealed partial class InventoryTransferAccessor
{
    [ExecuteScalar]
    public partial ValueTask<long> CountAsync(Guid? storeId, Guid? fromStoreId, Guid? toStoreId, InventoryTransferStatus? status, bool openOnly, CancellationToken cancellationToken);

    [Query]
    public partial ValueTask<List<InventoryTransferEntity>> QueryListAsync(Guid? storeId, Guid? fromStoreId, Guid? toStoreId, InventoryTransferStatus? status, bool openOnly, InventoryTransferSort sort, bool desc, int limit, int offset, CancellationToken cancellationToken);

    [QueryFirst]
    [SelectSingle(typeof(InventoryTransferEntity))]
    public partial ValueTask<InventoryTransferEntity?> QueryAsync(Guid id, CancellationToken cancellationToken);

    // 出荷店ごとの連番で移動番号を付けて登録する。1 文の INSERT ... SELECT で採番するので、同時に登録しても番号は重ならない (出荷店 × 連番の一意制約でも守る)
    [QueryFirst]
    public partial ValueTask<InventoryTransferEntity?> InsertAsync(
        DbTransaction tx,
        Guid id,
        Guid fromStoreId,
        string fromStoreCode,
        Guid toStoreId,
        string? note,
        DateTime now,
        CancellationToken cancellationToken);

    // 出荷 (依頼のときだけ)。行が返らなければ、ない・出荷済み・キャンセル済み
    [QueryFirst]
    public partial ValueTask<InventoryTransferEntity?> UpdateShippedAsync(DbTransaction tx, Guid id, DateTime shippedAt, Guid? staffId, DateTime updatedAt, CancellationToken cancellationToken);

    // 受領 (出荷済みのときだけ)
    [QueryFirst]
    public partial ValueTask<InventoryTransferEntity?> UpdateReceivedAsync(DbTransaction tx, Guid id, DateTime receivedAt, Guid? staffId, DateTime updatedAt, CancellationToken cancellationToken);

    // キャンセル (依頼のときだけ)
    [QueryFirst]
    public partial ValueTask<InventoryTransferEntity?> UpdateCancelledAsync(Guid id, DateTime cancelledAt, DateTime updatedAt, CancellationToken cancellationToken);

    //--------------------------------------------------------------------------------
    // Line
    //--------------------------------------------------------------------------------

    [Query]
    public partial ValueTask<List<InventoryTransferLineEntity>> QueryLineListAsync(Guid transferId, CancellationToken cancellationToken);

    [Execute]
    [Insert(typeof(InventoryTransferLineEntity))]
    public partial ValueTask<int> InsertLineAsync(DbTransaction tx, InventoryTransferLineEntity entity, CancellationToken cancellationToken);

    [Execute]
    public partial ValueTask<int> UpdateLineReceivedAsync(DbTransaction tx, Guid id, decimal receivedQuantity, CancellationToken cancellationToken);
}
