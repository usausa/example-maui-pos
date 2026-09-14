namespace Pos.Server.Models.Parameters;

// 商品一覧の絞り込み (keyword は code / barcode / name / kana / modelNo の部分一致)
public sealed class ProductQueryParameter : PagedParameter
{
    public Guid? CategoryId { get; init; }

    public string? Keyword { get; init; }

    public bool? IsActive { get; init; }

    public DateTime? UpdatedSince { get; init; }

    public bool IncludeDeleted { get; init; }
}
