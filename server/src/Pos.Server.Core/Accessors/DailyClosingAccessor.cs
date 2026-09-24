namespace Pos.Server.Accessors;

using Pos.Server.Models.Entity;
using Pos.Server.Models.Views;

[DataAccessor]
[ExecuteConfig(typeof(DataProfile))]
public sealed partial class DailyClosingAccessor
{
    //--------------------------------------------------------------------------------
    // Day
    //--------------------------------------------------------------------------------

    // 店舗 × 営業日 (シフト・取引・締めのある日)。その日のシフトと、その日の取引を含むシフトを数える
    [ExecuteScalar]
    public partial ValueTask<long> CountDaysAsync(Guid? storeId, DailyClosingStatus? status, DateOnly? from, DateOnly? to, CancellationToken cancellationToken);

    // 締め済みは締めた時点の日計、未締めは取引から集計する
    [Query]
    public partial ValueTask<List<DailyClosingDayView>> QueryDayListAsync(Guid? storeId, DailyClosingStatus? status, DateOnly? from, DateOnly? to, DailyClosingSort sort, bool desc, int limit, int offset, CancellationToken cancellationToken);

    // その営業日のシフトと、その営業日の取引を含むシフト (前日から日をまたいだシフト)
    [Query]
    public partial ValueTask<List<ShiftEntity>> QueryShiftListAsync(Guid storeId, DateOnly businessDate, CancellationToken cancellationToken);

    //--------------------------------------------------------------------------------
    // DailyClosing
    //--------------------------------------------------------------------------------

    [QueryFirst]
    [SelectSingle(typeof(DailyClosingEntity))]
    public partial ValueTask<DailyClosingEntity?> QueryAsync(Guid id, CancellationToken cancellationToken);

    [QueryFirst]
    public partial ValueTask<DailyClosingEntity?> QueryByBusinessDateAsync(Guid storeId, DateOnly businessDate, CancellationToken cancellationToken);

    // 店舗 × 営業日の一意インデックスで、締め済みなら重複エラーになる
    [Execute]
    [Insert(typeof(DailyClosingEntity))]
    public partial ValueTask<int> InsertAsync(DbTransaction tx, DailyClosingEntity entity, CancellationToken cancellationToken);

    [Execute]
    [Insert(typeof(DailyClosingPaymentEntity))]
    public partial ValueTask<int> InsertPaymentAsync(DbTransaction tx, DailyClosingPaymentEntity entity, CancellationToken cancellationToken);

    [Execute]
    [Insert(typeof(DailyClosingTaxEntity))]
    public partial ValueTask<int> InsertTaxAsync(DbTransaction tx, DailyClosingTaxEntity entity, CancellationToken cancellationToken);

    [Query]
    public partial ValueTask<List<PaymentMethodTotalView>> QueryPaymentListAsync(Guid dailyClosingId, CancellationToken cancellationToken);

    [Query]
    public partial ValueTask<List<TaxRateTotalView>> QueryTaxListAsync(Guid dailyClosingId, CancellationToken cancellationToken);

    // 締め後に届いた取引の印。戻り値 0 = 締めていない
    [Execute]
    public partial ValueTask<int> UpdateHasLateTransactionsAsync(DbTransaction tx, Guid storeId, DateOnly businessDate, DateTime updatedAt, CancellationToken cancellationToken);

    [Execute]
    public partial ValueTask<int> DeletePaymentsAsync(DbTransaction tx, Guid dailyClosingId, CancellationToken cancellationToken);

    [Execute]
    public partial ValueTask<int> DeleteTaxesAsync(DbTransaction tx, Guid dailyClosingId, CancellationToken cancellationToken);

    // 戻り値 0 = 見つからない (解除済み)
    [Execute]
    public partial ValueTask<int> DeleteAsync(DbTransaction tx, Guid id, CancellationToken cancellationToken);

    //--------------------------------------------------------------------------------
    // Summary
    //--------------------------------------------------------------------------------

    // 店舗 × 営業日の支払方法別 (取消済みを除く)
    [Query]
    public partial ValueTask<List<PaymentMethodTotalView>> QueryPaymentMethodSummaryAsync(Guid storeId, DateOnly businessDate, CancellationToken cancellationToken);

    // 店舗 × 営業日の税率別 (返品は負として合算)
    [Query]
    public partial ValueTask<List<TaxRateTotalView>> QueryTaxRateSummaryAsync(Guid storeId, DateOnly businessDate, CancellationToken cancellationToken);
}
