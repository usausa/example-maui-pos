namespace Pos.Domain.Enums;

// 棚卸・調整の登録の結果。Duplicate は同じ Id の再送で、登録済みの内容を返す
public enum InventoryChangeResultStatus
{
    Created,
    Duplicate
}
