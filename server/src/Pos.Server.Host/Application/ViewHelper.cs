namespace Pos.Server.Host.Application;

using MudBlazor;

using Pos.Server.Models;

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

    public static (string Text, Color Color, string? Icon) OrderTypeChip(OrderType type) => type switch
    {
        OrderType.BackOrder => ("取り寄せ", Color.Primary, Icons.Material.Filled.LocalShipping),
        OrderType.Hold => ("取り置き", Color.Secondary, Icons.Material.Filled.BookmarkAdded),
        _ => (type.ToString(), Color.Default, null)
    };

    // 受注: 入荷待ち → 引き渡し待ち (お客様への連絡が要る) → 完了
    public static (string Text, Color Color, string? Icon) OrderStatusChip(OrderStatus status) => status switch
    {
        OrderStatus.Ordered => ("入荷待ち", Color.Info, Icons.Material.Filled.HourglassTop),
        OrderStatus.Arrived => ("引き渡し待ち", Color.Warning, Icons.Material.Filled.Inventory),
        OrderStatus.Completed => ("完了", Color.Success, Icons.Material.Filled.CheckCircle),
        OrderStatus.Cancelled => ("キャンセル", Color.Default, Icons.Material.Filled.Cancel),
        _ => (status.ToString(), Color.Default, null)
    };

    // 入荷: 入荷予定 → 受領
    public static (string Text, Color Color, string? Icon) InventoryReceiptStatusChip(InventoryReceiptStatus status) => status switch
    {
        InventoryReceiptStatus.Draft => ("入荷予定", Color.Info, Icons.Material.Filled.HourglassTop),
        InventoryReceiptStatus.Received => ("受領済み", Color.Success, Icons.Material.Filled.CheckCircle),
        InventoryReceiptStatus.Cancelled => ("キャンセル", Color.Default, Icons.Material.Filled.Cancel),
        _ => (status.ToString(), Color.Default, null)
    };

    // 店舗間移動: 依頼 (出荷待ち) → 出荷済み (入荷店の受領待ち) → 受領
    public static (string Text, Color Color, string? Icon) InventoryTransferStatusChip(InventoryTransferStatus status) => status switch
    {
        InventoryTransferStatus.Requested => ("出荷待ち", Color.Info, Icons.Material.Filled.HourglassTop),
        InventoryTransferStatus.Shipped => ("受領待ち", Color.Warning, Icons.Material.Filled.LocalShipping),
        InventoryTransferStatus.Received => ("受領済み", Color.Success, Icons.Material.Filled.CheckCircle),
        InventoryTransferStatus.Cancelled => ("キャンセル", Color.Default, Icons.Material.Filled.Cancel),
        _ => (status.ToString(), Color.Default, null)
    };

    // 日次締め: 締めた後に同じ営業日の取引が届いた日は締め直しが要る
    public static (string Text, Color Color, string? Icon) DailyClosingChip(DailyClosingStatus status, bool hasLateTransactions) => (status, hasLateTransactions) switch
    {
        (DailyClosingStatus.Closed, true) => ("締め後の取引あり", Color.Warning, Icons.Material.Filled.Warning),
        (DailyClosingStatus.Closed, false) => ("締め済み", Color.Success, Icons.Material.Filled.Lock),
        _ => ("未締め", Color.Info, Icons.Material.Filled.LockOpen)
    };

    // 商品 CSV の取込の行の結果
    public static (string Text, Color Color, string? Icon) ImportActionChip(ImportAction action) => action switch
    {
        ImportAction.Insert => ("新規", Color.Success, Icons.Material.Filled.AddCircle),
        ImportAction.Update => ("更新", Color.Info, Icons.Material.Filled.Edit),
        ImportAction.Unchanged => ("変更なし", Color.Default, Icons.Material.Filled.Remove),
        ImportAction.Error => ("エラー", Color.Error, Icons.Material.Filled.Error),
        _ => (action.ToString(), Color.Default, null)
    };

    // 締めを止めている未精算のシフト
    public static (string Text, Color Color, string? Icon) OpenShiftChip(int count) => ($"未精算 {count}", Color.Warning, Icons.Material.Filled.PointOfSale);

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
        InventoryChangeType.Receive => ("入荷", Color.Success, Icons.Material.Filled.MoveToInbox),
        InventoryChangeType.TransferOut => ("移動出荷", Color.Tertiary, Icons.Material.Filled.CallMade),
        InventoryChangeType.TransferIn => ("移動受領", Color.Tertiary, Icons.Material.Filled.CallReceived),
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

    // 受領した数と予定・出荷の数の差 (受領前と一致は出さない)
    public static string QuantityDifferenceText(decimal quantity, decimal? receivedQuantity) =>
        receivedQuantity is { } received && received != quantity ? $"(差 {(received - quantity).ToDeltaText()})" : String.Empty;

    // 在庫変動の参照先 (取引・入荷・移動) の画面
    public static (string Href, string Text) ReferenceLink(string? referenceType, Guid referenceId) => referenceType switch
    {
        InventoryReferenceType.InventoryReceipt => ($"inventory/receipts?id={referenceId}", "📦 入荷"),
        InventoryReferenceType.InventoryTransfer => ($"inventory/transfers?id={referenceId}", "🚚 移動"),
        _ => ($"transactions?id={referenceId}", "🧾 取引")
    };

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
