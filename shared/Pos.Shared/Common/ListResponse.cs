namespace Pos.Shared.Common;

// 一覧応答の共通形 (api-design §2.2)。派生型は XxxListResponse
public abstract class ListResponse<T>
{
    public int Total { get; set; }

    public int Page { get; set; }

    public int Size { get; set; }

    public IReadOnlyList<T> Items { get; set; } = default!;
}
