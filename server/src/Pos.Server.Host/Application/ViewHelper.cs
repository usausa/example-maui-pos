namespace Pos.Server.Host.Application;

using MudBlazor;

// 状態を絵文字付きのチップで示す (文言と色の組)
public static class ViewHelper
{
    public static (string Text, Color Color) ActiveChip(bool isActive) =>
        isActive ? ("✅ 有効", Color.Success) : ("⏸ 停止", Color.Default);

    public static (string Text, Color Color) DeletedChip() => ("🗑 削除済み", Color.Dark);

    public static (string Text, Color Color) StatusChip(TransactionStatus status) => status switch
    {
        TransactionStatus.Completed => ("✅ 完了", Color.Success),
        TransactionStatus.Voided => ("❌ 取消", Color.Error),
        _ => (status.ToString(), Color.Default)
    };

    public static (string Text, Color Color) TypeChip(TransactionType type) => type switch
    {
        TransactionType.Sale => ("🛒 販売", Color.Primary),
        TransactionType.Return => ("↩️ 返品", Color.Warning),
        _ => (type.ToString(), Color.Default)
    };

    public static (string Text, Color Color) StatusChip(ShiftStatus status) => status switch
    {
        ShiftStatus.Open => ("🟢 開設中", Color.Success),
        ShiftStatus.Closed => ("🔒 精算済み", Color.Default),
        _ => (status.ToString(), Color.Default)
    };

    // 過不足: 0 は一致、正は過剰、負は不足
    public static (string Text, Color Color) DifferenceChip(decimal? difference) => difference switch
    {
        null => ("-", Color.Default),
        0m => ("✅ 一致", Color.Success),
        > 0m => ($"⚠️ +{difference.Value.ToYen()}", Color.Warning),
        _ => ($"🔴 {difference.Value.ToYen()}", Color.Error)
    };

    // 在庫数量: 負は要確認、0 は欠品
    public static (string Text, Color Color) QuantityChip(decimal quantity) => quantity switch
    {
        < 0m => ("🔴 マイナス", Color.Error),
        0m => ("⚠️ 欠品", Color.Warning),
        _ => ("✅ 在庫あり", Color.Success)
    };

    public static (string Text, Color Color) ChangeTypeChip(InventoryChangeType type) => type switch
    {
        InventoryChangeType.Sale => ("🛒 販売", Color.Primary),
        InventoryChangeType.Return => ("↩️ 返品", Color.Warning),
        InventoryChangeType.Void => ("❌ 取消", Color.Error),
        InventoryChangeType.PhysicalCount => ("📋 棚卸", Color.Info),
        InventoryChangeType.Adjustment => ("🔧 調整", Color.Secondary),
        _ => (type.ToString(), Color.Default)
    };

    public static (string Text, Color Color) PointTypeChip(PointHistoryType type) => type switch
    {
        PointHistoryType.Earn => ("➕ 付与", Color.Success),
        PointHistoryType.Redeem => ("➖ 利用", Color.Primary),
        PointHistoryType.Refund => ("↩️ 返還", Color.Warning),
        PointHistoryType.Revoke => ("↩️ 付与取消", Color.Warning),
        PointHistoryType.Void => ("❌ 取消", Color.Error),
        PointHistoryType.Adjust => ("🔧 調整", Color.Secondary),
        _ => (type.ToString(), Color.Default)
    };

    public static (string Text, Color Color) CashEventChip(CashEventType type) => type switch
    {
        CashEventType.PaidIn => ("⬇️ 入金", Color.Success),
        CashEventType.PaidOut => ("⬆️ 出金", Color.Warning),
        CashEventType.NoSale => ("🗄 ドロワ開", Color.Default),
        _ => (type.ToString(), Color.Default)
    };

    public static (string Text, Color Color) RoleChip(StaffRole role) => role switch
    {
        StaffRole.Admin => ("🛡 管理者", Color.Error),
        StaffRole.Manager => ("⭐ 店長", Color.Warning),
        StaffRole.Cashier => ("🧑 レジ担当", Color.Default),
        _ => (role.ToString(), Color.Default)
    };

    // 端末の最終通信: 5 分以内なら通信中
    public static (string Text, Color Color) OnlineChip(DateTime? lastSeenAt, DateTime utcNow) => lastSeenAt switch
    {
        null => ("⚪ 未接続", Color.Default),
        _ when utcNow - lastSeenAt.Value < TimeSpan.FromMinutes(5) => ("🟢 通信中", Color.Success),
        _ => ("⚪ " + lastSeenAt.ToDateTimeText(), Color.Default)
    };

    public static (string Text, Color Color) PointBalanceChip(int balance) =>
        balance < 0 ? ($"🔴 {balance:N0} pt", Color.Error) : ($"{balance:N0} pt", Color.Default);

    public static (string Text, Color Color) NegativeQuantityChip(decimal quantity) =>
        ($"🔴 {quantity.ToQuantityText()}", Color.Error);

    public static Color PointBalanceColor(int balance) => balance < 0 ? Color.Error : Color.Primary;

    // 関連取引: 取消済みは薄く
    public static Color RelatedTransactionColor(TransactionStatus status) => status == TransactionStatus.Voided ? Color.Default : Color.Warning;

    //--------------------------------------------------------------------------------
    // Mark / Label
    //--------------------------------------------------------------------------------

    public static string ApprovalMark(bool requiresApproval) => requiresApproval ? "🔑 承認要" : String.Empty;

    public static string ChangeMark(bool allowsChange) => allowsChange ? "💴 あり" : String.Empty;

    public static string ReferenceMark(bool requiresReference) => requiresReference ? "🔢 必須" : String.Empty;

    public static string DefaultMark(bool isDefault) => isDefault ? "⭐ 既定" : String.Empty;

    public static string DeletedMark(bool isDeleted) => isDeleted ? "🗑" : String.Empty;

    public static string CustomerMark(bool hasCustomer) => hasCustomer ? "👤" : String.Empty;

    // 売価変更した明細
    public static string PriceOverrideMark(decimal unitPrice, decimal listPrice) => unitPrice != listPrice ? " ✏️" : String.Empty;

    public static string TransactionDiscountMark(bool isTransactionDiscount) => isTransactionDiscount ? "(取引)" : String.Empty;

    public static string ReturnedText(decimal returnedQuantity) => returnedQuantity > 0 ? $"(返品済 {returnedQuantity.ToQuantityText()})" : String.Empty;

    // 上位 3 位はメダル
    public static string RankText(int rank) => rank switch
    {
        1 => "🥇",
        2 => "🥈",
        3 => "🥉",
        _ => rank.ToString(CultureInfo.InvariantCulture)
    };

    public static string SerialNumbersText(IEnumerable<string> serialNumbers) => String.Join(", ", serialNumbers);

    public static string DiscountValueLabel(DiscountType type) => type == DiscountType.Percent ? "率 (0.05 = 5%)" : "金額";

    public static string InventoryQuantityLabel(InventoryChangeType type) => type == InventoryChangeType.PhysicalCount ? "実数" : "増減";

    public static string TaxColumnHeader(SalesSummaryGroupBy groupBy) => groupBy == SalesSummaryGroupBy.TaxRate ? "課税対象 / 税額" : "税";
}
