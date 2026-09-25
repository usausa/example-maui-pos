namespace Pos.Server.Accessors;

using Pos.Server.Models.Entity;

// 入荷 (入荷予定と明細)。在庫の加減算と変動履歴は InventoryAccessor
[DataAccessor]
[ExecuteConfig(typeof(DataProfile))]
public sealed partial class InventoryReceiptAccessor
{
    [ExecuteScalar]
    public partial ValueTask<long> CountAsync(Guid? storeId, Guid? supplierId, InventoryReceiptStatus? status, DateOnly? from, DateOnly? to, CancellationToken cancellationToken);

    [Query]
    public partial ValueTask<List<InventoryReceiptEntity>> QueryListAsync(Guid? storeId, Guid? supplierId, InventoryReceiptStatus? status, DateOnly? from, DateOnly? to, InventoryReceiptSort sort, bool desc, int limit, int offset, CancellationToken cancellationToken);

    [QueryFirst]
    [SelectSingle(typeof(InventoryReceiptEntity))]
    public partial ValueTask<InventoryReceiptEntity?> QueryAsync(Guid id, CancellationToken cancellationToken);

    [Execute]
    [Insert(typeof(InventoryReceiptEntity))]
    public partial ValueTask<int> InsertAsync(DbTransaction tx, InventoryReceiptEntity entity, CancellationToken cancellationToken);

    // 受領 (入荷予定のときだけ)。行が返らなければ、ない・受領済み・キャンセル済み
    [QueryFirst]
    public partial ValueTask<InventoryReceiptEntity?> UpdateReceivedAsync(DbTransaction tx, Guid id, DateTime receivedAt, Guid? staffId, DateTime updatedAt, CancellationToken cancellationToken);

    // キャンセル (入荷予定のときだけ)
    [QueryFirst]
    public partial ValueTask<InventoryReceiptEntity?> UpdateCancelledAsync(DbTransaction tx, Guid id, DateTime cancelledAt, DateTime updatedAt, CancellationToken cancellationToken);

    //--------------------------------------------------------------------------------
    // Line
    //--------------------------------------------------------------------------------

    [Query]
    public partial ValueTask<List<InventoryReceiptLineEntity>> QueryLineListAsync(Guid receiptId, CancellationToken cancellationToken);

    [Execute]
    [Insert(typeof(InventoryReceiptLineEntity))]
    public partial ValueTask<int> InsertLineAsync(DbTransaction tx, InventoryReceiptLineEntity entity, CancellationToken cancellationToken);

    [Execute]
    public partial ValueTask<int> UpdateLineReceivedAsync(DbTransaction tx, Guid id, decimal receivedQuantity, CancellationToken cancellationToken);
}
