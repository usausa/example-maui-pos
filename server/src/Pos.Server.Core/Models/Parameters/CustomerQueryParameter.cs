namespace Pos.Server.Models.Parameters;

// 会員一覧の絞り込み (keyword は code / name / kana / phone の部分一致)
public sealed class CustomerQueryParameter : PagedParameter<CustomerSort>
{
    public string? Keyword { get; init; }

    public string? Code { get; init; }

    public string? Phone { get; init; }

    public DateTime? UpdatedSince { get; init; }

    public bool IncludeDeleted { get; init; }
}
