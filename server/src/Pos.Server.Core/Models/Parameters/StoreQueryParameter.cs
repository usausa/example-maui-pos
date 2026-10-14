namespace Pos.Server.Models.Parameters;

// 店舗一覧の絞り込み
public sealed class StoreQueryParameter : PagedParameter<StoreSort>
{
    public DateTime? UpdatedSince { get; init; }

    public bool IncludeDeleted { get; init; }
}
