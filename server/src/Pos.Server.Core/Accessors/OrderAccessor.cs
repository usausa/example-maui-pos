namespace Pos.Server.Accessors;

using Pos.Server.Models.Entity;
using Pos.Server.Models.Views;

// 受注 (取り寄せ・取り置き)。書き込みは呼び出し側が IDbProvider.UsingTxAsync で束ねる
[DataAccessor]
[ExecuteConfig(typeof(DataProfile))]
public sealed partial class OrderAccessor
{
    //--------------------------------------------------------------------------------
    // Order
    //--------------------------------------------------------------------------------

    // orderedFrom / orderedTo は受注日時 (UTC) の [from, to)
    [ExecuteScalar]
    public partial ValueTask<long> CountAsync(Guid? storeId, OrderStatus? status, bool openOnly, OrderType? type, Guid? customerId, string? keyword, DateTime? orderedFrom, DateTime? orderedTo, CancellationToken cancellationToken);

    [Query]
    public partial ValueTask<List<OrderEntity>> QueryListAsync(Guid? storeId, OrderStatus? status, bool openOnly, OrderType? type, Guid? customerId, string? keyword, DateTime? orderedFrom, DateTime? orderedTo, OrderSort sort, bool desc, int limit, int offset, CancellationToken cancellationToken);

    [QueryFirst]
    [SelectSingle(typeof(OrderEntity))]
    public partial ValueTask<OrderEntity?> QueryAsync(Guid id, CancellationToken cancellationToken);

    // 会計した取引から受注を引く
    [QueryFirst]
    public partial ValueTask<OrderEntity?> QueryByTransactionAsync(Guid transactionId, CancellationToken cancellationToken);

    // 未完了の受注の状態ごとの件数
    [Query]
    public partial ValueTask<List<OrderStatusCountView>> QueryStatusSummaryAsync(Guid? storeId, CancellationToken cancellationToken);

    // 店舗ごとの連番で受注番号を付けて登録する。1 文の INSERT ... SELECT で採番するので、同時に登録しても番号は重ならない (店舗 × 連番の一意制約でも守る)
    [QueryFirst]
    public partial ValueTask<OrderEntity?> InsertAsync(
        DbTransaction tx,
        Guid id,
        Guid storeId,
        string storeCode,
        Guid? terminalId,
        Guid staffId,
        Guid? customerId,
        string customerName,
        string? phone,
        OrderType type,
        OrderStatus status,
        DateOnly? requestedDate,
        string? note,
        decimal total,
        DateTime orderedAt,
        DateTime? arrivedAt,
        DateTime now,
        CancellationToken cancellationToken);

    // 連絡先・希望日・備考・合計の変更 (未完了のときだけ)。行が返らなければ、ない・完了済み・版の不一致
    [QueryFirst]
    public partial ValueTask<OrderEntity?> UpdateAsync(
        DbTransaction tx,
        Guid id,
        Guid? customerId,
        string customerName,
        string? phone,
        DateOnly? requestedDate,
        string? note,
        decimal total,
        DateTime updatedAt,
        int version,
        CancellationToken cancellationToken);

    // 入荷 (入荷待ちのときだけ)
    [QueryFirst]
    public partial ValueTask<OrderEntity?> UpdateArrivedAsync(Guid id, DateTime arrivedAt, DateTime updatedAt, CancellationToken cancellationToken);

    // キャンセル (未完了のときだけ)
    [QueryFirst]
    public partial ValueTask<OrderEntity?> UpdateCancelledAsync(Guid id, DateTime cancelledAt, string? cancelReason, DateTime updatedAt, CancellationToken cancellationToken);

    // 会計で完了にする (引き渡し待ちのときだけ)。戻り値 0 = 引き渡し待ちでない
    [Execute]
    public partial ValueTask<int> UpdateCompletedAsync(DbTransaction tx, Guid id, Guid transactionId, DateTime completedAt, DateTime updatedAt, CancellationToken cancellationToken);

    // 会計した取引を取り消したら引き渡し待ちに戻す
    [Execute]
    public partial ValueTask<int> UpdateReopenedAsync(DbTransaction tx, Guid transactionId, DateTime updatedAt, CancellationToken cancellationToken);

    //--------------------------------------------------------------------------------
    // OrderLine
    //--------------------------------------------------------------------------------

    [Query]
    public partial ValueTask<List<OrderLineEntity>> QueryLineListAsync(Guid orderId, CancellationToken cancellationToken);

    [Execute]
    [Insert(typeof(OrderLineEntity))]
    public partial ValueTask<int> InsertLineAsync(DbTransaction tx, OrderLineEntity entity, CancellationToken cancellationToken);

    // 変更では明細を置き換える
    [Execute]
    public partial ValueTask<int> DeleteLinesAsync(DbTransaction tx, Guid orderId, CancellationToken cancellationToken);
}
