namespace Pos.Terminal.Services;

using Pos.Terminal.Models.Entity;

using Smart.Data.Accessor.Attributes;

// ローカル DB。マスタは Pos.Contract の Response をそのまま保存する。ローカルのエンティティ ([Key] あり) のキーによる取得・削除は組み込みの属性で生成する
[DataAccessor]
[ExecuteConfig(typeof(DataProfile))]
public sealed partial class DataAccessor
{
    //--------------------------------------------------------------------------------
    // Initialize
    //--------------------------------------------------------------------------------

    [Execute]
    public partial ValueTask<int> ExecutePragmaAsync(DbConnection con);

    // スキーマ (Resources/Raw/Schema.sql の複数文) をそのまま実行する
    [DirectSql]
    [Execute]
    public partial ValueTask<int> ExecuteSchemaAsync(DbConnection con, string sql);

    //--------------------------------------------------------------------------------
    // Settings
    //--------------------------------------------------------------------------------

    [QueryFirst]
    public partial ValueTask<SettingsResponse?> QuerySettingsAsync();

    [Execute]
    public partial ValueTask<int> DeleteSettingsAsync(DbTransaction tx);

    [Execute]
    [Insert(typeof(SettingsResponse), Table = "Settings")]
    public partial ValueTask<int> InsertSettingsAsync(DbTransaction tx, SettingsResponse entity);

    //--------------------------------------------------------------------------------
    // Stores / Terminals / Staff
    //--------------------------------------------------------------------------------

    [QueryFirst]
    public partial ValueTask<StoreListResponseItem?> QueryStoreAsync(Guid id);

    [Execute]
    public partial ValueTask<int> DeleteStoreAsync(DbTransaction tx, Guid id);

    [Execute]
    [Insert(typeof(StoreListResponseItem), Table = "Stores")]
    public partial ValueTask<int> InsertStoreAsync(DbTransaction tx, StoreListResponseItem entity);

    [QueryFirst]
    public partial ValueTask<TerminalListResponseItem?> QueryTerminalAsync(Guid id);

    [Execute]
    public partial ValueTask<int> DeleteTerminalAsync(DbTransaction tx, Guid id);

    [Execute]
    [Insert(typeof(TerminalListResponseItem), Table = "Terminals")]
    public partial ValueTask<int> InsertTerminalAsync(DbTransaction tx, TerminalListResponseItem entity);

    // 所属店舗のスタッフ (全店舗所属も含む)
    [Query]
    public partial ValueTask<List<StaffListResponseItem>> QueryStaffListAsync(Guid storeId);

    [QueryFirst]
    public partial ValueTask<StaffListResponseItem?> QueryStaffAsync(Guid id);

    [Execute]
    public partial ValueTask<int> DeleteStaffAsync(DbTransaction tx, Guid id);

    [Execute]
    [Insert(typeof(StaffListResponseItem), Table = "Staff")]
    public partial ValueTask<int> InsertStaffAsync(DbTransaction tx, StaffListResponseItem entity);

    //--------------------------------------------------------------------------------
    // Categories / TaxRates / Discounts / PaymentMethods / AdjustmentReasons
    //--------------------------------------------------------------------------------

    [Query]
    public partial ValueTask<List<CategoryListResponseItem>> QueryCategoryListAsync();

    [Execute]
    public partial ValueTask<int> DeleteCategoryAsync(DbTransaction tx, Guid id);

    [Execute]
    [Insert(typeof(CategoryListResponseItem), Table = "Categories")]
    public partial ValueTask<int> InsertCategoryAsync(DbTransaction tx, CategoryListResponseItem entity);

    [Query]
    public partial ValueTask<List<TaxRateListResponseItem>> QueryTaxRateListAsync();

    [Execute]
    public partial ValueTask<int> DeleteTaxRateAsync(DbTransaction tx, Guid id);

    [Execute]
    [Insert(typeof(TaxRateListResponseItem), Table = "TaxRates")]
    public partial ValueTask<int> InsertTaxRateAsync(DbTransaction tx, TaxRateListResponseItem entity);

    [Query]
    public partial ValueTask<List<DiscountListResponseItem>> QueryDiscountListAsync();

    [Execute]
    public partial ValueTask<int> DeleteDiscountAsync(DbTransaction tx, Guid id);

    [Execute]
    [Insert(typeof(DiscountListResponseItem), Table = "Discounts")]
    public partial ValueTask<int> InsertDiscountAsync(DbTransaction tx, DiscountListResponseItem entity);

    [Query]
    public partial ValueTask<List<PaymentMethodListResponseItem>> QueryPaymentMethodListAsync();

    [Execute]
    public partial ValueTask<int> DeletePaymentMethodAsync(DbTransaction tx, Guid id);

    [Execute]
    [Insert(typeof(PaymentMethodListResponseItem), Table = "PaymentMethods")]
    public partial ValueTask<int> InsertPaymentMethodAsync(DbTransaction tx, PaymentMethodListResponseItem entity);

    [Query]
    public partial ValueTask<List<AdjustmentReasonListResponseItem>> QueryAdjustmentReasonListAsync();

    [Execute]
    public partial ValueTask<int> DeleteAdjustmentReasonAsync(DbTransaction tx, Guid id);

    [Execute]
    [Insert(typeof(AdjustmentReasonListResponseItem), Table = "AdjustmentReasons")]
    public partial ValueTask<int> InsertAdjustmentReasonAsync(DbTransaction tx, AdjustmentReasonListResponseItem entity);

    //--------------------------------------------------------------------------------
    // Products
    //--------------------------------------------------------------------------------

    [ExecuteScalar]
    public partial ValueTask<long> CountProductsAsync();

    [QueryFirst]
    public partial ValueTask<ProductListResponseItem?> QueryProductAsync(Guid id);

    [QueryFirst]
    public partial ValueTask<ProductListResponseItem?> QueryProductByBarcodeAsync(string barcode);

    [QueryFirst]
    public partial ValueTask<ProductListResponseItem?> QueryProductByCodeAsync(string code);

    // keyword は LIKE パターン (コード / JAN / 名称 / かな / 型番)
    [Query]
    public partial ValueTask<List<ProductListResponseItem>> QueryProductListAsync(Guid[]? categoryIds, string? keyword, int limit);

    [Execute]
    public partial ValueTask<int> DeleteProductAsync(DbTransaction tx, Guid id);

    [Execute]
    [Insert(typeof(ProductListResponseItem), Table = "Products")]
    public partial ValueTask<int> InsertProductAsync(DbTransaction tx, ProductListResponseItem entity);

    //--------------------------------------------------------------------------------
    // InventoryLevels (自店分)
    //--------------------------------------------------------------------------------

    [QueryFirst]
    public partial ValueTask<InventoryLevelListResponseItem?> QueryInventoryLevelAsync(Guid storeId, Guid productId);

    // 同期結果の反映 (UPSERT)
    [Execute]
    public partial ValueTask<int> UpsertInventoryLevelAsync(DbTransaction tx, Guid storeId, Guid productId, decimal quantity, DateTime updatedAt);

    // 販売・返品・棚卸の即時反映 (加減算の UPSERT)
    [Execute]
    public partial ValueTask<int> AddInventoryQuantityAsync(DbTransaction tx, Guid storeId, Guid productId, decimal delta, DateTime updatedAt);

    [Execute]
    public partial ValueTask<int> UpdateInventoryQuantityAsync(DbTransaction tx, Guid storeId, Guid productId, decimal quantity, DateTime updatedAt);

    //--------------------------------------------------------------------------------
    // Transactions
    //--------------------------------------------------------------------------------

    [Execute]
    [Insert(typeof(LocalTransactionEntity))]
    public partial ValueTask<int> InsertTransactionAsync(DbTransaction tx, LocalTransactionEntity entity);

    [Execute]
    public partial ValueTask<int> UpdateTransactionAsync(Guid id, TransactionStatus status, string payload);

    [Execute]
    public partial ValueTask<int> UpdateTransactionStatusAsync(DbTransaction tx, Guid id, TransactionStatus status, string payload);

    [QueryFirst]
    [SelectSingle(typeof(LocalTransactionEntity))]
    public partial ValueTask<LocalTransactionEntity?> QueryTransactionAsync(Guid id);

    [QueryFirst]
    public partial ValueTask<LocalTransactionEntity?> QueryTransactionByReceiptNoAsync(string receiptNo);

    [Query]
    public partial ValueTask<List<LocalTransactionEntity>> QueryTransactionListAsync(Guid? shiftId, DateOnly? businessDate, TransactionType? type, int limit);

    [Execute]
    public partial ValueTask<int> DeleteTransactionsBeforeAsync(DateOnly businessDate);

    //--------------------------------------------------------------------------------
    // Shifts / CashEvents
    //--------------------------------------------------------------------------------

    [Execute]
    [Insert(typeof(LocalShiftEntity))]
    public partial ValueTask<int> InsertShiftAsync(DbTransaction tx, LocalShiftEntity entity);

    // サーバにある開設中のシフトの行をそのまま登録する (1 文なのでトランザクションなし。Builder は同名のオーバーロードにできない)
    [Execute]
    [Insert(typeof(LocalShiftEntity))]
    public partial ValueTask<int> InsertServerShiftAsync(LocalShiftEntity entity);

    // 別の端末として登録し直したとき、開設を送っていないシフトを今の店舗・端末に移す
    [Execute]
    public partial ValueTask<int> UpdateShiftTerminalAsync(DbTransaction tx, Guid id, Guid storeId, Guid terminalId);

    // 精算の内容を書いて Closed にする
    [Execute]
    public partial ValueTask<int> UpdateShiftClosedAsync(DbTransaction tx, Guid id, DateTime closedAt, Guid closedByStaffId, decimal actualCash, decimal expectedCash, decimal difference, string? note);

    [QueryFirst]
    [SelectSingle(typeof(LocalShiftEntity))]
    public partial ValueTask<LocalShiftEntity?> QueryShiftAsync(Guid id);

    [QueryFirst]
    public partial ValueTask<LocalShiftEntity?> QueryCurrentShiftAsync(Guid terminalId);

    [Query]
    public partial ValueTask<List<LocalShiftEntity>> QueryShiftListAsync(int limit);

    [Execute]
    [Insert(typeof(LocalCashEventEntity))]
    public partial ValueTask<int> InsertCashEventAsync(DbTransaction tx, LocalCashEventEntity entity);

    [Query]
    public partial ValueTask<List<LocalCashEventEntity>> QueryCashEventListAsync(Guid shiftId);

    //--------------------------------------------------------------------------------
    // OrderDeposits
    //--------------------------------------------------------------------------------

    // サーバが受け付けた前受金を写す (写し済みの Id は何もしない。1 文なのでトランザクションなし)
    [Execute]
    public partial ValueTask<int> InsertOrderDepositAsync(Guid id, Guid orderId, Guid shiftId, OrderDepositType type, Guid paymentMethodId, PaymentKind kind, decimal amount, DateTime occurredAt);

    [Query]
    public partial ValueTask<List<LocalOrderDepositEntity>> QueryOrderDepositListAsync(Guid shiftId);

    //--------------------------------------------------------------------------------
    // Outbox
    //--------------------------------------------------------------------------------

    [Execute]
    [Insert(typeof(OutboxEntity))]
    public partial ValueTask<int> InsertOutboxAsync(DbTransaction tx, OutboxEntity entity);

    [QueryFirst]
    [SelectSingle(typeof(OutboxEntity))]
    public partial ValueTask<OutboxEntity?> QueryOutboxAsync(Guid id);

    // status 指定なしは未送信 (Pending / Failed) を発生順に
    [Query]
    public partial ValueTask<List<OutboxEntity>> QueryOutboxListAsync(OutboxStatus? status, int limit);

    [ExecuteScalar]
    public partial ValueTask<long> CountOutboxAsync(OutboxStatus status);

    [Execute]
    public partial ValueTask<int> UpdateOutboxAsync(Guid id, OutboxStatus status, int attempts, string? lastError, DateTime? sentAt);

    // 内容を書き換えて送り直す (Pending に戻す)
    [Execute]
    public partial ValueTask<int> UpdateOutboxPayloadAsync(DbTransaction tx, Guid id, string payload);

    [Execute]
    [Delete(typeof(OutboxEntity))]
    public partial ValueTask<int> DeleteOutboxAsync(Guid id);

    [Execute]
    public partial ValueTask<int> DeleteSentOutboxAsync(DateTime before);

    //--------------------------------------------------------------------------------
    // SyncState
    //--------------------------------------------------------------------------------

    [ExecuteScalar]
    public partial ValueTask<string?> QuerySyncStateAsync(string key);

    [Execute]
    public partial ValueTask<int> UpsertSyncStateAsync(string key, string value);

    [Execute]
    public partial ValueTask<int> DeleteSyncStateAsync(DbConnection con, string key);

    //--------------------------------------------------------------------------------
    // HoldCarts
    //--------------------------------------------------------------------------------

    [Execute]
    [Insert(typeof(HoldCartEntity))]
    public partial ValueTask<int> InsertHoldCartAsync(HoldCartEntity entity);

    [Query]
    public partial ValueTask<List<HoldCartEntity>> QueryHoldCartListAsync();

    [QueryFirst]
    [SelectSingle(typeof(HoldCartEntity))]
    public partial ValueTask<HoldCartEntity?> QueryHoldCartAsync(Guid id);

    [Execute]
    [Delete(typeof(HoldCartEntity))]
    public partial ValueTask<int> DeleteHoldCartAsync(Guid id);
}
