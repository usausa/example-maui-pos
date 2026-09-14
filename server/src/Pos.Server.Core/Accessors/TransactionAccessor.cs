namespace Pos.Server.Accessors;

using Pos.Server.Models.Entity;

// 取引一式。書き込みは呼び出し側が IDbProvider.UsingTxAsync で束ねる
[DataAccessor]
[ExecuteConfig(typeof(DataProfile))]
public sealed partial class TransactionAccessor
{
    [Execute]
    public partial void Create();

    //--------------------------------------------------------------------------------
    // Query
    //--------------------------------------------------------------------------------

    [QueryFirst]
    [SelectSingle(typeof(TransactionEntity), Table = "Transactions")]
    public partial ValueTask<TransactionEntity?> QueryAsync(Guid id, CancellationToken cancellationToken);

    [QueryFirst]
    public partial ValueTask<TransactionEntity?> QueryByReceiptNoAsync(string receiptNo, CancellationToken cancellationToken);

    [ExecuteScalar]
    public partial ValueTask<long> CountAsync(
        Guid? storeId,
        Guid? terminalId,
        Guid? staffId,
        Guid? shiftId,
        Guid? customerId,
        DateOnly? from,
        DateOnly? to,
        TransactionType? type,
        TransactionStatus? status,
        CancellationToken cancellationToken);

    [Query]
    public partial ValueTask<List<TransactionEntity>> QueryListAsync(
        Guid? storeId,
        Guid? terminalId,
        Guid? staffId,
        Guid? shiftId,
        Guid? customerId,
        DateOnly? from,
        DateOnly? to,
        TransactionType? type,
        TransactionStatus? status,
        string sort,
        int limit,
        int offset,
        CancellationToken cancellationToken);

    [Query]
    public partial ValueTask<List<TransactionLineEntity>> QueryLinesAsync(Guid transactionId, CancellationToken cancellationToken);

    [Query]
    public partial ValueTask<List<TransactionLineSerialEntity>> QueryLineSerialsAsync(Guid transactionId, CancellationToken cancellationToken);

    [Query]
    public partial ValueTask<List<TransactionDiscountEntity>> QueryDiscountsAsync(Guid transactionId, CancellationToken cancellationToken);

    [Query]
    public partial ValueTask<List<TransactionTaxSummaryEntity>> QueryTaxSummariesAsync(Guid transactionId, CancellationToken cancellationToken);

    [Query]
    public partial ValueTask<List<TransactionPaymentEntity>> QueryPaymentsAsync(Guid transactionId, CancellationToken cancellationToken);

    [QueryFirst]
    public partial ValueTask<TransactionDeliveryEntity?> QueryDeliveryAsync(Guid transactionId, CancellationToken cancellationToken);

    // 取消可否 (返品が紐付いていれば HAS_RETURNS)
    [ExecuteScalar]
    public partial ValueTask<long> CountReturnsAsync(Guid originalTransactionId, CancellationToken cancellationToken);

    // 元取引に紐付く返品取引 (取消済みも含む。管理画面の関連取引)
    [Query]
    public partial ValueTask<List<TransactionEntity>> QueryReturnsAsync(Guid originalTransactionId, CancellationToken cancellationToken);

    //--------------------------------------------------------------------------------
    // Insert
    //--------------------------------------------------------------------------------

    [Execute]
    [Insert(typeof(TransactionEntity), Table = "Transactions")]
    public partial ValueTask<int> InsertAsync(DbTransaction tx, TransactionEntity entity, CancellationToken cancellationToken);

    [Execute]
    [Insert(typeof(TransactionLineEntity), Table = "TransactionLines")]
    public partial ValueTask<int> InsertLineAsync(DbTransaction tx, TransactionLineEntity entity, CancellationToken cancellationToken);

    [Execute]
    [Insert(typeof(TransactionLineSerialEntity), Table = "TransactionLineSerials")]
    public partial ValueTask<int> InsertLineSerialAsync(DbTransaction tx, TransactionLineSerialEntity entity, CancellationToken cancellationToken);

    [Execute]
    [Insert(typeof(TransactionDiscountEntity), Table = "TransactionDiscounts")]
    public partial ValueTask<int> InsertDiscountAsync(DbTransaction tx, TransactionDiscountEntity entity, CancellationToken cancellationToken);

    [Execute]
    [Insert(typeof(TransactionTaxSummaryEntity), Table = "TransactionTaxSummaries")]
    public partial ValueTask<int> InsertTaxSummaryAsync(DbTransaction tx, TransactionTaxSummaryEntity entity, CancellationToken cancellationToken);

    [Execute]
    [Insert(typeof(TransactionPaymentEntity), Table = "TransactionPayments")]
    public partial ValueTask<int> InsertPaymentAsync(DbTransaction tx, TransactionPaymentEntity entity, CancellationToken cancellationToken);

    [Execute]
    [Insert(typeof(TransactionDeliveryEntity), Table = "TransactionDeliveries")]
    public partial ValueTask<int> InsertDeliveryAsync(DbTransaction tx, TransactionDeliveryEntity entity, CancellationToken cancellationToken);

    //--------------------------------------------------------------------------------
    // Update
    //--------------------------------------------------------------------------------

    // 取消。戻り値 0 = 既に取消済み
    [Execute]
    public partial ValueTask<int> VoidAsync(DbTransaction tx, Guid id, DateTime voidedAt, Guid voidedByStaffId, string reason, DateTime updatedAt, CancellationToken cancellationToken);

    // 処理後残高 (ポイント更新後に確定)
    [Execute]
    public partial ValueTask<int> UpdatePointsBalanceAfterAsync(DbTransaction tx, Guid id, int? pointsBalanceAfter, CancellationToken cancellationToken);

    // 返品数量を元明細に加算 (超過するときは更新されず 0 が返る)。取消の戻しは負の quantity
    [Execute]
    public partial ValueTask<int> AddReturnedQuantityAsync(DbTransaction tx, Guid lineId, decimal quantity, CancellationToken cancellationToken);
}
