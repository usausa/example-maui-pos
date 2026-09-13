namespace Pos.Server.Host.Application;

using MudBlazor;

// 状態を絵文字付きのチップで示す (文言と色の組)
public static class ChipText
{
    public static (string Text, Color Color) Active(bool isActive) =>
        isActive ? ("✅ 有効", Color.Success) : ("⏸ 停止", Color.Default);

    public static (string Text, Color Color) Deleted() => ("🗑 削除済み", Color.Dark);

    public static (string Text, Color Color) Status(TransactionStatus status) => status switch
    {
        TransactionStatus.Completed => ("✅ 完了", Color.Success),
        TransactionStatus.Voided => ("❌ 取消", Color.Error),
        _ => (status.ToString(), Color.Default)
    };

    public static (string Text, Color Color) Type(TransactionType type) => type switch
    {
        TransactionType.Sale => ("🛒 販売", Color.Primary),
        TransactionType.Return => ("↩️ 返品", Color.Warning),
        _ => (type.ToString(), Color.Default)
    };

    public static (string Text, Color Color) Status(ShiftStatus status) => status switch
    {
        ShiftStatus.Open => ("🟢 開設中", Color.Success),
        ShiftStatus.Closed => ("🔒 精算済み", Color.Default),
        _ => (status.ToString(), Color.Default)
    };

    // 過不足: 0 は一致、正は過剰、負は不足
    public static (string Text, Color Color) Difference(decimal? difference) => difference switch
    {
        null => ("-", Color.Default),
        0m => ("✅ 一致", Color.Success),
        > 0m => ($"⚠️ +{DisplayText.Yen(difference.Value)}", Color.Warning),
        _ => ($"🔴 {DisplayText.Yen(difference.Value)}", Color.Error)
    };

    // 在庫数量: 負は要確認、0 は欠品
    public static (string Text, Color Color) Quantity(decimal quantity) => quantity switch
    {
        < 0m => ("🔴 マイナス", Color.Error),
        0m => ("⚠️ 欠品", Color.Warning),
        _ => ("✅ 在庫あり", Color.Success)
    };

    public static (string Text, Color Color) ChangeType(InventoryChangeType type) => type switch
    {
        InventoryChangeType.Sale => ("🛒 販売", Color.Primary),
        InventoryChangeType.Return => ("↩️ 返品", Color.Warning),
        InventoryChangeType.Void => ("❌ 取消", Color.Error),
        InventoryChangeType.PhysicalCount => ("📋 棚卸", Color.Info),
        InventoryChangeType.Adjustment => ("🔧 調整", Color.Secondary),
        _ => (type.ToString(), Color.Default)
    };

    public static (string Text, Color Color) PointType(PointHistoryType type) => type switch
    {
        PointHistoryType.Earn => ("➕ 付与", Color.Success),
        PointHistoryType.Redeem => ("➖ 利用", Color.Primary),
        PointHistoryType.Refund => ("↩️ 返還", Color.Warning),
        PointHistoryType.Revoke => ("↩️ 付与取消", Color.Warning),
        PointHistoryType.Void => ("❌ 取消", Color.Error),
        PointHistoryType.Adjust => ("🔧 調整", Color.Secondary),
        _ => (type.ToString(), Color.Default)
    };

    public static (string Text, Color Color) CashEvent(CashEventType type) => type switch
    {
        CashEventType.PaidIn => ("⬇️ 入金", Color.Success),
        CashEventType.PaidOut => ("⬆️ 出金", Color.Warning),
        CashEventType.NoSale => ("🗄 ドロワ開", Color.Default),
        _ => (type.ToString(), Color.Default)
    };

    public static (string Text, Color Color) Role(StaffRole role) => role switch
    {
        StaffRole.Admin => ("🛡 管理者", Color.Error),
        StaffRole.Manager => ("⭐ 店長", Color.Warning),
        StaffRole.Cashier => ("🧑 レジ担当", Color.Default),
        _ => (role.ToString(), Color.Default)
    };

    // 端末の最終通信: 5 分以内なら通信中
    public static (string Text, Color Color) Online(DateTime? lastSeenAt, DateTime utcNow) => lastSeenAt switch
    {
        null => ("⚪ 未接続", Color.Default),
        _ when utcNow - lastSeenAt.Value < TimeSpan.FromMinutes(5) => ("🟢 通信中", Color.Success),
        _ => ("⚪ " + DisplayText.DateTime(lastSeenAt), Color.Default)
    };

    public static (string Text, Color Color) PointBalance(int balance) =>
        balance < 0 ? ($"🔴 {balance:N0} pt", Color.Error) : ($"{balance:N0} pt", Color.Default);
}
