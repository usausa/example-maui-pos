namespace Pos.Terminal.Services;

using Pos.Terminal.Helpers.Data;
using Pos.Terminal.Models.Entity;

using Smart.Data.Accessor.Attributes;

// ローカル DB (db-design §6)。マスタは Pos.Shared の Response をそのまま保存する
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
    public partial ValueTask<StoreResponse?> QueryStoreAsync(Guid id);

    [Execute]
    public partial ValueTask<int> DeleteStoreAsync(DbTransaction tx, Guid id);

    [Execute]
    [Insert(typeof(StoreResponse), Table = "Stores")]
    public partial ValueTask<int> InsertStoreAsync(DbTransaction tx, StoreResponse entity);

    [QueryFirst]
    public partial ValueTask<TerminalResponse?> QueryTerminalAsync(Guid id);

    [Execute]
    public partial ValueTask<int> DeleteTerminalAsync(DbTransaction tx, Guid id);

    [Execute]
    [Insert(typeof(TerminalResponse), Table = "Terminals")]
    public partial ValueTask<int> InsertTerminalAsync(DbTransaction tx, TerminalResponse entity);

    // 所属店舗のスタッフ (全店舗所属も含む)
    [Query]
    public partial ValueTask<List<StaffResponse>> QueryStaffListAsync(Guid storeId);

    [QueryFirst]
    public partial ValueTask<StaffResponse?> QueryStaffAsync(Guid id);

    [Execute]
    public partial ValueTask<int> DeleteStaffAsync(DbTransaction tx, Guid id);

    [Execute]
    [Insert(typeof(StaffResponse), Table = "Staff")]
    public partial ValueTask<int> InsertStaffAsync(DbTransaction tx, StaffResponse entity);

    //--------------------------------------------------------------------------------
    // Categories / TaxRates / Discounts / PaymentMethods / AdjustmentReasons
    //--------------------------------------------------------------------------------

    [Query]
    public partial ValueTask<List<CategoryResponse>> QueryCategoryListAsync();

    [Execute]
    public partial ValueTask<int> DeleteCategoryAsync(DbTransaction tx, Guid id);

    [Execute]
    [Insert(typeof(CategoryResponse), Table = "Categories")]
    public partial ValueTask<int> InsertCategoryAsync(DbTransaction tx, CategoryResponse entity);

    [Query]
    public partial ValueTask<List<TaxRateResponse>> QueryTaxRateListAsync();

    [Execute]
    public partial ValueTask<int> DeleteTaxRateAsync(DbTransaction tx, Guid id);

    [Execute]
    [Insert(typeof(TaxRateResponse), Table = "TaxRates")]
    public partial ValueTask<int> InsertTaxRateAsync(DbTransaction tx, TaxRateResponse entity);

    [Query]
    public partial ValueTask<List<DiscountResponse>> QueryDiscountListAsync();

    [Execute]
    public partial ValueTask<int> DeleteDiscountAsync(DbTransaction tx, Guid id);

    [Execute]
    [Insert(typeof(DiscountResponse), Table = "Discounts")]
    public partial ValueTask<int> InsertDiscountAsync(DbTransaction tx, DiscountResponse entity);

    [Query]
    public partial ValueTask<List<PaymentMethodResponse>> QueryPaymentMethodListAsync();

    [Execute]
    public partial ValueTask<int> DeletePaymentMethodAsync(DbTransaction tx, Guid id);

    [Execute]
    [Insert(typeof(PaymentMethodResponse), Table = "PaymentMethods")]
    public partial ValueTask<int> InsertPaymentMethodAsync(DbTransaction tx, PaymentMethodResponse entity);

    [Query]
    public partial ValueTask<List<AdjustmentReasonResponse>> QueryAdjustmentReasonListAsync();

    [Execute]
    public partial ValueTask<int> DeleteAdjustmentReasonAsync(DbTransaction tx, Guid id);

    [Execute]
    [Insert(typeof(AdjustmentReasonResponse), Table = "AdjustmentReasons")]
    public partial ValueTask<int> InsertAdjustmentReasonAsync(DbTransaction tx, AdjustmentReasonResponse entity);

    //--------------------------------------------------------------------------------
    // Products
    //--------------------------------------------------------------------------------

    [ExecuteScalar]
    public partial ValueTask<long> CountProductsAsync();

    [QueryFirst]
    public partial ValueTask<ProductResponse?> QueryProductAsync(Guid id);

    [QueryFirst]
    public partial ValueTask<ProductResponse?> QueryProductByBarcodeAsync(string barcode);

    [QueryFirst]
    public partial ValueTask<ProductResponse?> QueryProductByCodeAsync(string code);

    // keyword は LIKE パターン (コード / JAN / 名称 / かな / 型番)
    [Query]
    public partial ValueTask<List<ProductResponse>> QueryProductListAsync(Guid[]? categoryIds, string? keyword, int limit);

    [Execute]
    public partial ValueTask<int> DeleteProductAsync(DbTransaction tx, Guid id);

    [Execute]
    [Insert(typeof(ProductResponse), Table = "Products")]
    public partial ValueTask<int> InsertProductAsync(DbTransaction tx, ProductResponse entity);

    //--------------------------------------------------------------------------------
    // InventoryLevels (自店分)
    //--------------------------------------------------------------------------------

    [QueryFirst]
    public partial ValueTask<InventoryLevelResponse?> QueryInventoryLevelAsync(Guid storeId, Guid productId);

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
    [Insert(typeof(LocalTransactionEntity), Table = "Transactions")]
    public partial ValueTask<int> InsertTransactionAsync(DbTransaction tx, LocalTransactionEntity entity);

    [Execute]
    public partial ValueTask<int> UpdateTransactionAsync(Guid id, TransactionStatus status, string payload);

    [Execute]
    public partial ValueTask<int> UpdateTransactionStatusAsync(DbTransaction tx, Guid id, TransactionStatus status, string payload);

    [QueryFirst]
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
    [Insert(typeof(LocalShiftEntity), Table = "Shifts")]
    public partial ValueTask<int> InsertShiftAsync(DbTransaction tx, LocalShiftEntity entity);

    [Execute]
    public partial ValueTask<int> CloseShiftAsync(DbTransaction tx, Guid id, DateTime closedAt, Guid closedByStaffId, decimal actualCash, decimal expectedCash, decimal difference, string? note);

    [QueryFirst]
    public partial ValueTask<LocalShiftEntity?> QueryShiftAsync(Guid id);

    [QueryFirst]
    public partial ValueTask<LocalShiftEntity?> QueryCurrentShiftAsync(Guid terminalId);

    [Query]
    public partial ValueTask<List<LocalShiftEntity>> QueryShiftListAsync(int limit);

    [Execute]
    [Insert(typeof(LocalCashEventEntity), Table = "CashEvents")]
    public partial ValueTask<int> InsertCashEventAsync(DbTransaction tx, LocalCashEventEntity entity);

    [Query]
    public partial ValueTask<List<LocalCashEventEntity>> QueryCashEventListAsync(Guid shiftId);

    //--------------------------------------------------------------------------------
    // Outbox
    //--------------------------------------------------------------------------------

    [Execute]
    [Insert(typeof(OutboxEntity), Table = "Outbox")]
    public partial ValueTask<int> InsertOutboxAsync(DbTransaction tx, OutboxEntity entity);

    [QueryFirst]
    public partial ValueTask<OutboxEntity?> QueryOutboxAsync(Guid id);

    // status 指定なしは未送信 (Pending / Failed) を発生順に
    [Query]
    public partial ValueTask<List<OutboxEntity>> QueryOutboxListAsync(OutboxStatus? status, int limit);

    [ExecuteScalar]
    public partial ValueTask<long> CountOutboxAsync(OutboxStatus status);

    [Execute]
    public partial ValueTask<int> UpdateOutboxAsync(Guid id, OutboxStatus status, int attempts, string? lastError, DateTime? sentAt);

    [Execute]
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
    [Insert(typeof(HoldCartEntity), Table = "HoldCarts")]
    public partial ValueTask<int> InsertHoldCartAsync(HoldCartEntity entity);

    [Query]
    public partial ValueTask<List<HoldCartEntity>> QueryHoldCartListAsync();

    [QueryFirst]
    public partial ValueTask<HoldCartEntity?> QueryHoldCartAsync(Guid id);

    [Execute]
    public partial ValueTask<int> DeleteHoldCartAsync(Guid id);
}
