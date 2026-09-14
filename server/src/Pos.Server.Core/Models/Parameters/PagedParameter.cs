namespace Pos.Server.Models.Parameters;

// 一覧の並び順とページ。Sort は各サービスが許可した列だけを使う
public abstract class PagedParameter
{
    public string? Sort { get; init; }

    public bool Desc { get; init; }

    public int Page { get; init; }

    public int Size { get; init; } = 20;
}
