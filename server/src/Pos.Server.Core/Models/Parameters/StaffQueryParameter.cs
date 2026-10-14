namespace Pos.Server.Models.Parameters;

// スタッフ一覧の絞り込み
public sealed class StaffQueryParameter : PagedParameter<StaffSort>
{
    public Guid? StoreId { get; init; }

    public DateTime? UpdatedSince { get; init; }

    public bool IncludeDeleted { get; init; }
}
