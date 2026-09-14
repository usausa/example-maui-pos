namespace Pos.Server.Models.Parameters;

// 現在庫一覧 (管理画面) の絞り込み。keyword は商品のコード / JAN / 名称 / かなの部分一致
public sealed class InventoryLevelDetailQueryParameter : PagedParameter
{
    public Guid? StoreId { get; init; }

    public Guid? CategoryId { get; init; }

    public string? Keyword { get; init; }

    public bool NegativeOnly { get; init; }
}
