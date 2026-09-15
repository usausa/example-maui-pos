namespace Pos.Server.Models.Parameters;

// シフト一覧の絞り込み (from / to は営業日)
public sealed class ShiftQueryParameter : PagedParameter<ShiftSort>
{
    public Guid? StoreId { get; init; }

    public Guid? TerminalId { get; init; }

    public ShiftStatus? Status { get; init; }

    public DateOnly? From { get; init; }

    public DateOnly? To { get; init; }
}
