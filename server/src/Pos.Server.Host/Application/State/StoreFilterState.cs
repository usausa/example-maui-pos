namespace Pos.Server.Host.Application.State;

// 一覧ページ間で共有する店舗の絞り込み (回線ごと。null は全店舗)
public sealed class StoreFilterState
{
    public Guid? StoreId { get; set; }
}
