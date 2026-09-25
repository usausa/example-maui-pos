namespace Pos.Server.Accessors;

using Pos.Server.Models.Entity;

// 発注 (発注と明細)。発注で作る入荷予定は InventoryReceiptAccessor
[DataAccessor]
[ExecuteConfig(typeof(DataProfile))]
public sealed partial class PurchaseOrderAccessor
{
    [ExecuteScalar]
    public partial ValueTask<long> CountAsync(Guid? storeId, Guid? supplierId, PurchaseOrderStatus? status, bool openOnly, DateOnly? from, DateOnly? to, CancellationToken cancellationToken);

    [Query]
    public partial ValueTask<List<PurchaseOrderEntity>> QueryListAsync(Guid? storeId, Guid? supplierId, PurchaseOrderStatus? status, bool openOnly, DateOnly? from, DateOnly? to, PurchaseOrderSort sort, bool desc, int limit, int offset, CancellationToken cancellationToken);

    [QueryFirst]
    [SelectSingle(typeof(PurchaseOrderEntity))]
    public partial ValueTask<PurchaseOrderEntity?> QueryAsync(Guid id, CancellationToken cancellationToken);

    // 入荷予定を作った発注 (発注から作っていない入荷予定は null)
    [QueryFirst]
    public partial ValueTask<PurchaseOrderEntity?> QueryByReceiptIdAsync(Guid receiptId, CancellationToken cancellationToken);

    // 店舗ごとの連番で発注番号を付けて下書きで登録する。1 文の INSERT ... SELECT で採番するので、同時に登録しても番号は重ならない (店舗 × 連番の一意制約でも守る)
    [QueryFirst]
    public partial ValueTask<PurchaseOrderEntity?> InsertAsync(
        DbTransaction tx,
        Guid id,
        Guid storeId,
        string storeCode,
        Guid supplierId,
        DateOnly? expectedDate,
        string? note,
        DateTime now,
        CancellationToken cancellationToken);

    // 変更 (下書きのときだけ)。行が返らなければ、ない・下書きでない・版の不一致
    [QueryFirst]
    public partial ValueTask<PurchaseOrderEntity?> UpdateAsync(
        DbTransaction tx,
        Guid id,
        Guid supplierId,
        DateOnly? expectedDate,
        string? note,
        DateTime updatedAt,
        int version,
        CancellationToken cancellationToken);

    // 発注 (下書きのときだけ)。作った入荷予定を持つ
    [QueryFirst]
    public partial ValueTask<PurchaseOrderEntity?> UpdateOrderedAsync(DbTransaction tx, Guid id, DateTime orderedAt, string? orderedBy, Guid receiptId, DateTime updatedAt, CancellationToken cancellationToken);

    // キャンセル (下書きか発注済みのときだけ)
    [QueryFirst]
    public partial ValueTask<PurchaseOrderEntity?> UpdateCancelledAsync(DbTransaction tx, Guid id, DateTime cancelledAt, DateTime updatedAt, CancellationToken cancellationToken);

    // 入荷予定の受領とキャンセルに合わせる (発注済みのときだけ。発注から作っていない入荷予定は 0 件)
    [Execute]
    public partial ValueTask<int> UpdateReceivedByReceiptIdAsync(DbTransaction tx, Guid receiptId, DateTime updatedAt, CancellationToken cancellationToken);

    [Execute]
    public partial ValueTask<int> UpdateCancelledByReceiptIdAsync(DbTransaction tx, Guid receiptId, DateTime cancelledAt, DateTime updatedAt, CancellationToken cancellationToken);

    //--------------------------------------------------------------------------------
    // Line
    //--------------------------------------------------------------------------------

    [Query]
    public partial ValueTask<List<PurchaseOrderLineEntity>> QueryLineListAsync(Guid purchaseOrderId, CancellationToken cancellationToken);

    [Execute]
    [Insert(typeof(PurchaseOrderLineEntity))]
    public partial ValueTask<int> InsertLineAsync(DbTransaction tx, PurchaseOrderLineEntity entity, CancellationToken cancellationToken);

    [Execute]
    public partial ValueTask<int> DeleteLinesAsync(DbTransaction tx, Guid purchaseOrderId, CancellationToken cancellationToken);
}
