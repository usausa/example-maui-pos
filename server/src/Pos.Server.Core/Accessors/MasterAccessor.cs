namespace Pos.Server.Accessors;

using Pos.Server.Models.Entity;
using Pos.Server.Models.Views;

// 会社設定と各マスタ。更新は Version が一致する行だけ (楽観ロック)、削除は論理削除。戻り値 0 = 競合または削除済み。
// 更新の引数は列ごとに渡す (Entity のプロパティ参照では型変換が効かないため)
[DataAccessor]
[ExecuteConfig(typeof(DataProfile))]
public sealed partial class MasterAccessor
{
    [Execute]
    public partial void Create();

    // 後から増えた列を既存 DB に足す (足したときは true。呼び出し側で初期値を入れる)
    public static ValueTask<bool> EnsurePaymentMethodShortNameAsync(DbConnection con, CancellationToken cancellationToken) =>
        SqlHelper.EnsureColumnAsync(con, "PaymentMethods", "ShortName", "TEXT", cancellationToken);

    //--------------------------------------------------------------------------------
    // Settings (1 行、Id = 1)
    //--------------------------------------------------------------------------------

    [QueryFirst]
    public partial ValueTask<SettingsEntity?> QuerySettingsAsync(CancellationToken cancellationToken);

    [Execute]
    [Insert(typeof(SettingsEntity), Table = "Settings")]
    public partial ValueTask<int> InsertSettingsAsync(SettingsEntity entity, CancellationToken cancellationToken);

    [Execute]
    public partial ValueTask<int> UpdateSettingsAsync(
        string companyName,
        string currency,
        TaxRounding taxRounding,
        PointBasis pointBasis,
        string businessDayStartTime,
        DateTime updatedAt,
        int version,
        CancellationToken cancellationToken);

    //--------------------------------------------------------------------------------
    // Store
    //--------------------------------------------------------------------------------

    [ExecuteScalar]
    public partial ValueTask<long> CountStoresAsync(DateTime? updatedSince, bool includeDeleted, CancellationToken cancellationToken);

    [Query]
    public partial ValueTask<List<StoreEntity>> QueryStoreListAsync(DateTime? updatedSince, bool includeDeleted, string sort, int limit, int offset, CancellationToken cancellationToken);

    // 全件 (コード順)
    [Query]
    public partial ValueTask<List<StoreEntity>> QueryStoreAllAsync(bool includeDeleted, CancellationToken cancellationToken);

    [QueryFirst]
    [SelectSingle(typeof(StoreEntity), Table = "Stores")]
    public partial ValueTask<StoreEntity?> QueryStoreAsync(Guid id, CancellationToken cancellationToken);

    [Execute]
    [Insert(typeof(StoreEntity), Table = "Stores")]
    public partial ValueTask<int> InsertStoreAsync(StoreEntity entity, CancellationToken cancellationToken);

    [Execute]
    public partial ValueTask<int> UpdateStoreAsync(
        Guid id,
        string code,
        string name,
        string? postalCode,
        string? address,
        string? phone,
        string? registrationNo,
        string? receiptHeader,
        string? receiptFooter,
        string timeZone,
        bool isActive,
        DateTime updatedAt,
        int version,
        CancellationToken cancellationToken);

    [Execute]
    public partial ValueTask<int> DeleteStoreAsync(Guid id, DateTime updatedAt, CancellationToken cancellationToken);

    //--------------------------------------------------------------------------------
    // Terminal
    //--------------------------------------------------------------------------------

    [ExecuteScalar]
    public partial ValueTask<long> CountTerminalsAsync(Guid? storeId, DateTime? updatedSince, bool includeDeleted, CancellationToken cancellationToken);

    [Query]
    public partial ValueTask<List<TerminalEntity>> QueryTerminalListAsync(Guid? storeId, DateTime? updatedSince, bool includeDeleted, string sort, int limit, int offset, CancellationToken cancellationToken);

    // 全件 (店舗、端末番号順)
    [Query]
    public partial ValueTask<List<TerminalEntity>> QueryTerminalAllAsync(bool includeDeleted, CancellationToken cancellationToken);

    [QueryFirst]
    [SelectSingle(typeof(TerminalEntity), Table = "Terminals")]
    public partial ValueTask<TerminalEntity?> QueryTerminalAsync(Guid id, CancellationToken cancellationToken);

    [Execute]
    [Insert(typeof(TerminalEntity), Table = "Terminals")]
    public partial ValueTask<int> InsertTerminalAsync(TerminalEntity entity, CancellationToken cancellationToken);

    [Execute]
    public partial ValueTask<int> UpdateTerminalAsync(
        Guid id,
        Guid storeId,
        int terminalNo,
        string name,
        bool isActive,
        DateTime updatedAt,
        int version,
        CancellationToken cancellationToken);

    [Execute]
    public partial ValueTask<int> DeleteTerminalAsync(Guid id, DateTime updatedAt, CancellationToken cancellationToken);

    // 取引登録時: 最終レシート連番を max(現在値, 今回) に更新し、最終通信時刻を記録する
    [Execute]
    public partial ValueTask<int> UpdateTerminalLastReceiptSeqAsync(DbTransaction tx, Guid id, int receiptSeq, DateTime seenAt, CancellationToken cancellationToken);

    // 削除可否 (開設中シフトがあれば IN_USE)
    [ExecuteScalar]
    public partial ValueTask<long> CountTerminalOpenShiftsAsync(Guid terminalId, CancellationToken cancellationToken);

    //--------------------------------------------------------------------------------
    // Staff
    //--------------------------------------------------------------------------------

    // storeId 指定時は本部 (StoreId = NULL) も含める
    [ExecuteScalar]
    public partial ValueTask<long> CountStaffAsync(Guid? storeId, DateTime? updatedSince, bool includeDeleted, CancellationToken cancellationToken);

    [Query]
    public partial ValueTask<List<StaffEntity>> QueryStaffListAsync(Guid? storeId, DateTime? updatedSince, bool includeDeleted, string sort, int limit, int offset, CancellationToken cancellationToken);

    // 全件 (コード順)
    [Query]
    public partial ValueTask<List<StaffEntity>> QueryStaffAllAsync(bool includeDeleted, CancellationToken cancellationToken);

    [QueryFirst]
    [SelectSingle(typeof(StaffEntity), Table = "Staff")]
    public partial ValueTask<StaffEntity?> QueryStaffAsync(Guid id, CancellationToken cancellationToken);

    [Execute]
    [Insert(typeof(StaffEntity), Table = "Staff")]
    public partial ValueTask<int> InsertStaffAsync(StaffEntity entity, CancellationToken cancellationToken);

    [Execute]
    public partial ValueTask<int> UpdateStaffAsync(
        Guid id,
        string code,
        string name,
        StaffRole role,
        Guid? storeId,
        bool isActive,
        DateTime updatedAt,
        int version,
        CancellationToken cancellationToken);

    [Execute]
    public partial ValueTask<int> DeleteStaffAsync(Guid id, DateTime updatedAt, CancellationToken cancellationToken);

    //--------------------------------------------------------------------------------
    // Category
    //--------------------------------------------------------------------------------

    [ExecuteScalar]
    public partial ValueTask<long> CountCategoriesAsync(DateTime? updatedSince, bool includeDeleted, CancellationToken cancellationToken);

    [Query]
    public partial ValueTask<List<CategoryEntity>> QueryCategoryListAsync(DateTime? updatedSince, bool includeDeleted, string sort, int limit, int offset, CancellationToken cancellationToken);

    // 全件 (並び順、コード順)
    [Query]
    public partial ValueTask<List<CategoryEntity>> QueryCategoryAllAsync(bool includeDeleted, CancellationToken cancellationToken);

    [QueryFirst]
    [SelectSingle(typeof(CategoryEntity), Table = "Categories")]
    public partial ValueTask<CategoryEntity?> QueryCategoryAsync(Guid id, CancellationToken cancellationToken);

    [Execute]
    [Insert(typeof(CategoryEntity), Table = "Categories")]
    public partial ValueTask<int> InsertCategoryAsync(CategoryEntity entity, CancellationToken cancellationToken);

    [Execute]
    public partial ValueTask<int> UpdateCategoryAsync(
        Guid id,
        string code,
        string name,
        Guid? parentId,
        int sortOrder,
        DateTime updatedAt,
        int version,
        CancellationToken cancellationToken);

    [Execute]
    public partial ValueTask<int> DeleteCategoryAsync(Guid id, DateTime updatedAt, CancellationToken cancellationToken);

    // 削除可否 (所属商品・子部門があれば IN_USE)
    [ExecuteScalar]
    public partial ValueTask<long> CountCategoryProductsAsync(Guid categoryId, CancellationToken cancellationToken);

    [ExecuteScalar]
    public partial ValueTask<long> CountCategoryChildrenAsync(Guid parentId, CancellationToken cancellationToken);

    // 部門ごとの所属商品数 (管理画面のツリー)
    [Query]
    public partial ValueTask<List<CategoryProductCount>> QueryCategoryProductCountsAsync(CancellationToken cancellationToken);

    //--------------------------------------------------------------------------------
    // TaxRate (少数なのでページングなし。SortOrder, Code 順)
    //--------------------------------------------------------------------------------

    [Query]
    public partial ValueTask<List<TaxRateEntity>> QueryTaxRateListAsync(DateTime? updatedSince, bool includeDeleted, CancellationToken cancellationToken);

    [QueryFirst]
    [SelectSingle(typeof(TaxRateEntity), Table = "TaxRates")]
    public partial ValueTask<TaxRateEntity?> QueryTaxRateAsync(Guid id, CancellationToken cancellationToken);

    [Execute]
    [Insert(typeof(TaxRateEntity), Table = "TaxRates")]
    public partial ValueTask<int> InsertTaxRateAsync(TaxRateEntity entity, CancellationToken cancellationToken);

    [Execute]
    public partial ValueTask<int> UpdateTaxRateAsync(
        Guid id,
        string code,
        string name,
        decimal rate,
        TaxKind kind,
        bool isDefault,
        int sortOrder,
        DateTime updatedAt,
        int version,
        CancellationToken cancellationToken);

    [Execute]
    public partial ValueTask<int> DeleteTaxRateAsync(Guid id, DateTime updatedAt, CancellationToken cancellationToken);

    // 既定は 1 件だけ: 指定 ID 以外の IsDefault を落とす
    [Execute]
    public partial ValueTask<int> ClearDefaultTaxRateAsync(Guid exceptId, DateTime updatedAt, CancellationToken cancellationToken);

    // 削除可否 (使用中商品があれば IN_USE)
    [ExecuteScalar]
    public partial ValueTask<long> CountTaxRateProductsAsync(Guid taxRateId, CancellationToken cancellationToken);

    //--------------------------------------------------------------------------------
    // Discount
    //--------------------------------------------------------------------------------

    [Query]
    public partial ValueTask<List<DiscountEntity>> QueryDiscountListAsync(DateTime? updatedSince, bool includeDeleted, CancellationToken cancellationToken);

    [QueryFirst]
    [SelectSingle(typeof(DiscountEntity), Table = "Discounts")]
    public partial ValueTask<DiscountEntity?> QueryDiscountAsync(Guid id, CancellationToken cancellationToken);

    [Execute]
    [Insert(typeof(DiscountEntity), Table = "Discounts")]
    public partial ValueTask<int> InsertDiscountAsync(DiscountEntity entity, CancellationToken cancellationToken);

    [Execute]
    public partial ValueTask<int> UpdateDiscountAsync(
        Guid id,
        string code,
        string name,
        DiscountType type,
        decimal value,
        DiscountScope scope,
        bool requiresApproval,
        bool isActive,
        int sortOrder,
        DateTime updatedAt,
        int version,
        CancellationToken cancellationToken);

    [Execute]
    public partial ValueTask<int> DeleteDiscountAsync(Guid id, DateTime updatedAt, CancellationToken cancellationToken);

    //--------------------------------------------------------------------------------
    // PaymentMethod
    //--------------------------------------------------------------------------------

    [Query]
    public partial ValueTask<List<PaymentMethodEntity>> QueryPaymentMethodListAsync(DateTime? updatedSince, bool includeDeleted, CancellationToken cancellationToken);

    [QueryFirst]
    [SelectSingle(typeof(PaymentMethodEntity), Table = "PaymentMethods")]
    public partial ValueTask<PaymentMethodEntity?> QueryPaymentMethodAsync(Guid id, CancellationToken cancellationToken);

    [Execute]
    [Insert(typeof(PaymentMethodEntity), Table = "PaymentMethods")]
    public partial ValueTask<int> InsertPaymentMethodAsync(PaymentMethodEntity entity, CancellationToken cancellationToken);

    [Execute]
    public partial ValueTask<int> UpdatePaymentMethodAsync(
        Guid id,
        string code,
        string name,
        string? shortName,
        PaymentKind kind,
        bool allowsChange,
        bool requiresReference,
        bool isActive,
        int sortOrder,
        DateTime updatedAt,
        int version,
        CancellationToken cancellationToken);

    [Execute]
    public partial ValueTask<int> DeletePaymentMethodAsync(Guid id, DateTime updatedAt, CancellationToken cancellationToken);

    // Kind = Points かつ有効な行はちょうど 1 件 (指定 ID を除いた件数)
    [ExecuteScalar]
    public partial ValueTask<long> CountActivePointsPaymentMethodsAsync(Guid exceptId, CancellationToken cancellationToken);

    //--------------------------------------------------------------------------------
    // AdjustmentReason
    //--------------------------------------------------------------------------------

    [Query]
    public partial ValueTask<List<AdjustmentReasonEntity>> QueryAdjustmentReasonListAsync(DateTime? updatedSince, bool includeDeleted, CancellationToken cancellationToken);

    [QueryFirst]
    [SelectSingle(typeof(AdjustmentReasonEntity), Table = "AdjustmentReasons")]
    public partial ValueTask<AdjustmentReasonEntity?> QueryAdjustmentReasonAsync(Guid id, CancellationToken cancellationToken);

    [Execute]
    [Insert(typeof(AdjustmentReasonEntity), Table = "AdjustmentReasons")]
    public partial ValueTask<int> InsertAdjustmentReasonAsync(AdjustmentReasonEntity entity, CancellationToken cancellationToken);

    [Execute]
    public partial ValueTask<int> UpdateAdjustmentReasonAsync(
        Guid id,
        string code,
        string name,
        int sortOrder,
        bool isActive,
        DateTime updatedAt,
        int version,
        CancellationToken cancellationToken);

    [Execute]
    public partial ValueTask<int> DeleteAdjustmentReasonAsync(Guid id, DateTime updatedAt, CancellationToken cancellationToken);
}
