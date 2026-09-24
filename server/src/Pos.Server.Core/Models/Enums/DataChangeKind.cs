namespace Pos.Server.Models.Enums;

// 変更の通知の種類 (管理画面が読み直す範囲の目安)
public enum DataChangeKind
{
    Transaction,
    Shift,
    Inventory,
    Order,
    DailyClosing
}
