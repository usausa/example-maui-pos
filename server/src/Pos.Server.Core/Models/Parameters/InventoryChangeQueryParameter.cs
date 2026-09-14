namespace Pos.Server.Models.Parameters;

// 在庫変動履歴の絞り込み (from / to は UTC 日時、to は含まない)
public sealed class InventoryChangeQueryParameter
{
    public Guid? StoreId { get; init; }

    public Guid? ProductId { get; init; }

    public InventoryChangeType? Type { get; init; }

    public DateTime? From { get; init; }

    public DateTime? To { get; init; }

    public int Page { get; init; }

    public int Size { get; init; } = 20;
}
