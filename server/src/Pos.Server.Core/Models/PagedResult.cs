namespace Pos.Server.Models;

// 一覧のページ (総件数付き)
public sealed record PagedResult<T>(int Total, int Page, int Size, IReadOnlyList<T> Items);
