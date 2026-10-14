namespace Pos.Server.Models.Parameters;

// 端末一覧の絞り込み
public sealed class TerminalQueryParameter : PagedParameter<TerminalSort>
{
    public Guid? StoreId { get; init; }

    public DateTime? UpdatedSince { get; init; }

    public bool IncludeDeleted { get; init; }
}
