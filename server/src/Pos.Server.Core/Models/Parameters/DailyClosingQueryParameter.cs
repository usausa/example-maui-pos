namespace Pos.Server.Models.Parameters;

// 日次締め一覧の絞り込み (from / to は営業日)
public sealed class DailyClosingQueryParameter : PagedParameter<DailyClosingSort>
{
    public Guid? StoreId { get; init; }

    public DailyClosingStatus? Status { get; init; }

    public DateOnly? From { get; init; }

    public DateOnly? To { get; init; }
}
