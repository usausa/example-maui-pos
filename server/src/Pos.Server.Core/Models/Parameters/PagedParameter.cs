namespace Pos.Server.Models.Parameters;

// 一覧の並び順とページ。並び順は資源ごとの列挙型 (先頭が既定)
public abstract class PagedParameter<TSort>
    where TSort : struct, Enum
{
    public TSort Sort { get; init; }

    public bool Desc { get; init; }

    public int Page { get; init; }

    public int Size { get; init; } = 20;
}
