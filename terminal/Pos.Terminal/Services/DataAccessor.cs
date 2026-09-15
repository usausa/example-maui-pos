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

    [Execute]
    public partial ValueTask<int> CreateTablesAsync(DbConnection con);

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
    public partial ValueTask<StoreResponseItem?> QueryStoreAsync(Guid id);

    [Execute]
    public partial ValueTask<int> DeleteStoreAsync(DbTransaction tx, Guid id);

    [Execute]
    [Insert(typeof(StoreResponseItem), Table = "Stores")]
    public partial ValueTask<int> InsertStoreAsync(DbTransaction tx, StoreResponseItem entity);

    [QueryFirst]
    public partial ValueTask<TerminalResponseItem?> QueryTerminalAsync(Guid id);

    [Execute]
    public partial ValueTask<int> DeleteTerminalAsync(DbTransaction tx, Guid id);

    [Execute]
    [Insert(typeof(TerminalResponseItem), Table = "Terminals")]
    public partial ValueTask<int> InsertTerminalAsync(DbTransaction tx, TerminalResponseItem entity);

    // 所属店舗のスタッフ (全店舗所属も含む)
    [Query]
    public partial ValueTask<List<StaffResponseItem>> QueryStaffListAsync(Guid storeId);

    [QueryFirst]
    public partial ValueTask<StaffResponseItem?> QueryStaffAsync(Guid id);

    [Execute]
    public partial ValueTask<int> DeleteStaffAsync(DbTransaction tx, Guid id);

    [Execute]
    [Insert(typeof(StaffResponseItem), Table = "Staff")]
    public partial ValueTask<int> InsertStaffAsync(DbTransaction tx, StaffResponseItem entity);

    //--------------------------------------------------------------------------------
    // Categories / TaxRates / Discounts / PaymentMethods / AdjustmentReasons
    //--------------------------------------------------------------------------------

    [Query]
    public partial ValueTask<List<CategoryResponseItem>> QueryCategoryListAsync();

    [Execute]
    public partial ValueTask<int> DeleteCategoryAsync(DbTransaction tx, Guid id);

    [Execute]
    [Insert(typeof(CategoryResponseItem), Table = "Categories")]
    public partial ValueTask<int> InsertCategoryAsync(DbTransaction tx, CategoryResponseItem entity);

    [Query]
    public partial ValueTask<List<TaxRateResponseItem>> QueryTaxRateListAsync();

    [Execute]
    public partial ValueTask<int> DeleteTaxRateAsync(DbTransaction tx, Guid id);

    [Execute]
    [Insert(typeof(TaxRateResponseItem), Table = "TaxRates")]
    public partial ValueTask<int> InsertTaxRateAsync(DbTransaction tx, TaxRateResponseItem entity);

    [Query]
    public partial ValueTask<List<DiscountResponseItem>> QueryDiscountListAsync();

    [Execute]
    public partial ValueTask<int> DeleteDiscountAsync(DbTransaction tx, Guid id);

    [Execute]
    [Insert(typeof(DiscountResponseItem), Table = "Discounts")]
    public partial ValueTask<int> InsertDiscountAsync(DbTransaction tx, DiscountResponseItem entity);

    [Query]
    public partial ValueTask<List<PaymentMethodResponseItem>> QueryPaymentMethodListAsync();

    [Execute]
    public partial ValueTask<int> DeletePaymentMethodAsync(DbTransaction tx, Guid id);

    [Execute]
    [Insert(typeof(PaymentMethodResponseItem), Table = "PaymentMethods")]
    public partial ValueTask<int> InsertPaymentMethodAsync(DbTransaction tx, PaymentMethodResponseItem entity);

    [Query]
    public partial ValueTask<List<AdjustmentReasonResponseItem>> QueryAdjustmentReasonListAsync();

    [Execute]
    public partial ValueTask<int> DeleteAdjustmentReasonAsync(DbTransaction tx, Guid id);

    [Execute]
    [Insert(typeof(AdjustmentReasonResponseItem), Table = "AdjustmentReasons")]
    public partial ValueTask<int> InsertAdjustmentReasonAsync(DbTransaction tx, AdjustmentReasonResponseItem entity);

    //--------------------------------------------------------------------------------
    // Products
    //--------------------------------------------------------------------------------

    [ExecuteScalar]
    public partial ValueTask<long> CountProductsAsync();

    [QueryFirst]
    public partial ValueTask<ProductResponseItem?> QueryProductAsync(Guid id);

    [QueryFirst]
    public partial ValueTask<ProductResponseItem?> QueryProductByBarcodeAsync(string barcode);

    [QueryFirst]
    public partial ValueTask<ProductResponseItem?> QueryProductByCodeAsync(string code);

    // keyword は LIKE パターン (コード / JAN / 名称 / かな / 型番)
    [Query]
    public partial ValueTask<List<ProductResponseItem>> QueryProductListAsync(Guid[]? categoryIds, string? keyword, int limit);

    [Execute]
    public partial ValueTask<int> DeleteProductAsync(DbTransaction tx, Guid id);

    [Execute]
    [Insert(typeof(ProductResponseItem), Table = "Products")]
    public partial ValueTask<int> InsertProductAsync(DbTransaction tx, ProductResponseItem entity);

    //--------------------------------------------------------------------------------
    // InventoryLevels (自店分)
    //--------------------------------------------------------------------------------

    [QueryFirst]
    public partial ValueTask<InventoryLevelResponseItem?> QueryInventoryLevelAsync(Guid storeId, Guid productId);

    // 同期結果の反映 (UPSERT)
    [Execute]
    public partial ValueTask<int> UpsertInventoryLevelAsync(DbTransaction tx, Guid storeId, Guid productId, decimal quantity, DateTime updatedAt);

    // 販売・返品・棚卸の即時反映 (加減算の UPSERT)
    [Execute]
    public partial ValueTask<int> AddInventoryQuantityAsync(DbTransaction tx, Guid storeId, Guid productId, decimal delta, DateTime updatedAt);

    [Execute]
    public partial ValueTask<int> SetInventoryQuantityAsync(DbTransaction tx, Guid storeId, Guid productId, decimal quantity, DateTime updatedAt);

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
