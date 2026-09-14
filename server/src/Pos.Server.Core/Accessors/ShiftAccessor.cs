namespace Pos.Server.Accessors;

using Pos.Server.Models.Entity;
using Pos.Server.Models.Views;

[DataAccessor]
[ExecuteConfig(typeof(DataProfile))]
public sealed partial class ShiftAccessor
{
    [Execute]
    public partial void Create();

    //--------------------------------------------------------------------------------
    // Shift
    //--------------------------------------------------------------------------------

    // 端末の開設中シフト (なければ null)
    [QueryFirst]
    public partial ValueTask<ShiftEntity?> QueryCurrentAsync(Guid terminalId, CancellationToken cancellationToken);

    [ExecuteScalar]
    public partial ValueTask<long> CountAsync(Guid? storeId, Guid? terminalId, ShiftStatus? status, DateOnly? from, DateOnly? to, CancellationToken cancellationToken);

    [Query]
    public partial ValueTask<List<ShiftEntity>> QueryListAsync(Guid? storeId, Guid? terminalId, ShiftStatus? status, DateOnly? from, DateOnly? to, string sort, int limit, int offset, CancellationToken cancellationToken);

    [QueryFirst]
    [SelectSingle(typeof(ShiftEntity), Table = "Shifts")]
    public partial ValueTask<ShiftEntity?> QueryAsync(Guid id, CancellationToken cancellationToken);

    // 開設。端末に Open のシフトがあれば部分ユニークインデックスで重複エラーになる
    [Execute]
    [Insert(typeof(ShiftEntity), Table = "Shifts")]
    public partial ValueTask<int> InsertAsync(ShiftEntity entity, CancellationToken cancellationToken);

    // 精算: 集計を確定して Closed にする。戻り値 0 = 既に精算済み
    [Execute]
    public partial ValueTask<int> CloseAsync(
        DbTransaction tx,
        Guid id,
        DateTime closedAt,
        Guid closedByStaffId,
        decimal actualCash,
        decimal expectedCash,
        decimal difference,
        ShiftTotals totals,
        string? note,
        DateTime updatedAt,
        CancellationToken cancellationToken);

    [Execute]
    [Insert(typeof(ShiftDenominationEntity), Table = "ShiftDenominations")]
    public partial ValueTask<int> InsertDenominationAsync(DbTransaction tx, ShiftDenominationEntity entity, CancellationToken cancellationToken);

    [Query]
    public partial ValueTask<List<ShiftDenominationEntity>> QueryDenominationsAsync(Guid shiftId, CancellationToken cancellationToken);

    //--------------------------------------------------------------------------------
    // CashEvent
    //--------------------------------------------------------------------------------

    [Execute]
    [Insert(typeof(CashEventEntity), Table = "CashEvents")]
    public partial ValueTask<int> InsertCashEventAsync(CashEventEntity entity, CancellationToken cancellationToken);

    [QueryFirst]
    [SelectSingle(typeof(CashEventEntity), Table = "CashEvents")]
    public partial ValueTask<CashEventEntity?> QueryCashEventAsync(Guid id, CancellationToken cancellationToken);

    [ExecuteScalar]
    public partial ValueTask<long> CountCashEventsAsync(Guid shiftId, CancellationToken cancellationToken);

    [Query]
    public partial ValueTask<List<CashEventEntity>> QueryCashEventListAsync(Guid shiftId, int limit, int offset, CancellationToken cancellationToken);

    //--------------------------------------------------------------------------------
    // Summary
    //--------------------------------------------------------------------------------

    [QueryFirst]
    public partial ValueTask<ShiftTotals?> QueryTotalsAsync(Guid shiftId, CancellationToken cancellationToken);

    [Query]
    public partial ValueTask<List<PaymentMethodTotal>> QueryPaymentMethodTotalsAsync(Guid shiftId, CancellationToken cancellationToken);

    [Query]
    public partial ValueTask<List<TaxRateTotal>> QueryTaxRateTotalsAsync(Guid shiftId, CancellationToken cancellationToken);

    [Query]
    public partial ValueTask<List<CategoryTotal>> QueryCategoryTotalsAsync(Guid shiftId, CancellationToken cancellationToken);

    [QueryFirst]
    public partial ValueTask<PointTotals?> QueryPointTotalsAsync(Guid shiftId, CancellationToken cancellationToken);
}
