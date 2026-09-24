namespace Pos.Server.Host.Application;

using MudBlazor;

// 状態をチップで示す (文言・色・アイコンの組)。塗りつぶしのチップの上では色付きの絵文字が背景に溶けるため、記号は単色の Material Icons にする
public static class ViewHelper
{
    public static (string Text, Color Color, string? Icon) ActiveChip(bool isActive) =>
        isActive ? ("有効", Color.Success, Icons.Material.Filled.CheckCircle) : ("停止", Color.Default, Icons.Material.Filled.PauseCircle);

    public static (string Text, Color Color, string? Icon) DeletedChip() => ("削除済み", Color.Dark, Icons.Material.Filled.Delete);

    public static (string Text, Color Color, string? Icon) StatusChip(TransactionStatus status) => status switch
    {
        TransactionStatus.Completed => ("完了", Color.Success, Icons.Material.Filled.CheckCircle),
        TransactionStatus.Voided => ("取消", Color.Error, Icons.Material.Filled.Cancel),
        _ => (status.ToString(), Color.Default, null)
    };

    public static (string Text, Color Color, string? Icon) TypeChip(TransactionType type) => type switch
    {
        TransactionType.Sale => ("販売", Color.Primary, Icons.Material.Filled.ShoppingCart),
        TransactionType.Return => ("返品", Color.Warning, Icons.Material.Filled.Undo),
        _ => (type.ToString(), Color.Default, null)
    };

    public static (string Text, Color Color, string? Icon) StatusChip(ShiftStatus status) => status switch
    {
        ShiftStatus.Open => ("開設中", Color.Success, Icons.Material.Filled.LockOpen),
        ShiftStatus.Closed => ("精算済み", Color.Default, Icons.Material.Filled.Lock),
        _ => (status.ToString(), Color.Default, null)
    };

    // 過不足: 0 は一致、正は過剰、負は不足
    public static (string Text, Color Color, string? Icon) DifferenceChip(decimal? difference) => difference switch
    {
        null => ("-", Color.Default, null),
        0m => ("一致", Color.Success, Icons.Material.Filled.CheckCircle),
        > 0m => ($"+{difference.Value.ToYen()}", Color.Warning, Icons.Material.Filled.Warning),
        _ => (difference.Value.ToYen(), Color.Error, Icons.Material.Filled.Error)
    };

    // 在庫数量: 負は要確認、0 は欠品
    public static (string Text, Color Color, string? Icon) QuantityChip(decimal quantity) => quantity switch
    {
        < 0m => ("マイナス", Color.Error, Icons.Material.Filled.Error),
        0m => ("欠品", Color.Warning, Icons.Material.Filled.Warning),
        _ => ("在庫あり", Color.Success, Icons.Material.Filled.CheckCircle)
    };

    public static (string Text, Color Color, string? Icon) ChangeTypeChip(InventoryChangeType type) => type switch
    {
        InventoryChangeType.Sale => ("販売", Color.Primary, Icons.Material.Filled.ShoppingCart),
        InventoryChangeType.Return => ("返品", Color.Warning, Icons.Material.Filled.Undo),
        InventoryChangeType.Void => ("取消", Color.Error, Icons.Material.Filled.Cancel),
        InventoryChangeType.PhysicalCount => ("棚卸", Color.Info, Icons.Material.Filled.Assignment),
        InventoryChangeType.Adjustment => ("調整", Color.Secondary, Icons.Material.Filled.Build),
        _ => (type.ToString(), Color.Default, null)
    };

    public static (string Text, Color Color, string? Icon) PointTypeChip(PointHistoryType type) => type switch
    {
        PointHistoryType.Earn => ("付与", Color.Success, Icons.Material.Filled.Add),
        PointHistoryType.Redeem => ("利用", Color.Primary, Icons.Material.Filled.Remove),
        PointHistoryType.Refund => ("返還", Color.Warning, Icons.Material.Filled.Undo),
        PointHistoryType.Revoke => ("付与取消", Color.Warning, Icons.Material.Filled.Undo),
        PointHistoryType.Void => ("取消", Color.Error, Icons.Material.Filled.Cancel),
        PointHistoryType.Adjust => ("調整", Color.Secondary, Icons.Material.Filled.Build),
        _ => (type.ToString(), Color.Default, null)
    };

    public static (string Text, Color Color, string? Icon) CashEventChip(CashEventType type) => type switch
    {
        CashEventType.PaidIn => ("入金", Color.Success, Icons.Material.Filled.ArrowDownward),
        CashEventType.PaidOut => ("出金", Color.Warning, Icons.Material.Filled.ArrowUpward),
        CashEventType.NoSale => ("ドロワ開", Color.Default, Icons.Material.Filled.PointOfSale),
        _ => (type.ToString(), Color.Default, null)
    };

    public static (string Text, Color Color, string? Icon) RoleChip(StaffRole role) => role switch
    {
        StaffRole.Admin => ("管理者", Color.Error, Icons.Material.Filled.Shield),
        StaffRole.Manager => ("店長", Color.Warning, Icons.Material.Filled.Star),
        StaffRole.Cashier => ("レジ担当", Color.Default, Icons.Material.Filled.Person),
        _ => (role.ToString(), Color.Default, null)
    };

    // 端末の最終通信: 5 分以内なら通信中
    // 通信中かどうかは TerminalService.IsOnline で判定する (表示側は時計を持たない)
    public static (string Text, Color Color, string? Icon) OnlineChip(DateTime? lastSeenAt, bool online) => lastSeenAt switch
    {
        null => ("未接続", Color.Default, Icons.Material.Filled.WifiOff),
        _ when online => ("通信中", Color.Success, Icons.Material.Filled.Wifi),
        { } seen => ("通信なし " + seen.ToShortDateTimeText(), Color.Default, Icons.Material.Filled.WifiOff)
    };

    public static (string Text, Color Color, string? Icon) PointBalanceChip(int balance) =>
        balance < 0 ? ($"{balance:N0} pt", Color.Error, Icons.Material.Filled.Error) : ($"{balance:N0} pt", Color.Default, null);

    public static (string Text, Color Color, string? Icon) NegativeBalanceChip() => ("残高がマイナスです", Color.Error, Icons.Material.Filled.Error);

    public static (string Text, Color Color, string? Icon) NegativeQuantityChip(decimal quantity) =>
        (quantity.ToQuantityText(), Color.Error, Icons.Material.Filled.Error);

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

    public static string DeletedMark(bool isDeleted) => isDeleted ? "🗑️" : String.Empty;

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
