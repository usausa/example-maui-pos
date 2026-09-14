namespace Pos.Contract;

// 一覧応答の共通形。派生型は XxxResponse (要素は XxxResponseItem)
public abstract class ListResponse<T>
{
    public int Total { get; set; }

    public int Page { get; set; }

    public int Size { get; set; }

    public IReadOnlyList<T> Items { get; set; } = default!;
}
