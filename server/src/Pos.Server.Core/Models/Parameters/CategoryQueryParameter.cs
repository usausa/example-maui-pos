namespace Pos.Server.Models.Parameters;

// 部門一覧の絞り込み
public sealed class CategoryQueryParameter : PagedParameter<CategorySort>
{
    public DateTime? UpdatedSince { get; init; }

    public bool IncludeDeleted { get; init; }
}
