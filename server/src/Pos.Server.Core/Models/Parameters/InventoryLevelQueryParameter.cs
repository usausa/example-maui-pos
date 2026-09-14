namespace Pos.Server.Models.Parameters;

// 在庫一覧 (API) の絞り込み。差分同期 (updatedSince) は更新日時順、通常は商品コード順
public sealed class InventoryLevelQueryParameter
{
    public Guid? StoreId { get; init; }

    public Guid? ProductId { get; init; }

    public Guid? CategoryId { get; init; }

    public bool NegativeOnly { get; init; }

    public DateTime? UpdatedSince { get; init; }

    public int Page { get; init; }

    public int Size { get; init; } = 20;
}
