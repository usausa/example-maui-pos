namespace Pos.Server.Models.Parameters;

// 取引一覧の絞り込み (from / to は営業日)
public sealed class TransactionQueryParameter : PagedParameter<TransactionSort>
{
    public Guid? StoreId { get; init; }

    public Guid? TerminalId { get; init; }

    public Guid? StaffId { get; init; }

    public Guid? ShiftId { get; init; }

    public Guid? CustomerId { get; init; }

    public DateOnly? From { get; init; }

    public DateOnly? To { get; init; }

    public TransactionType? Type { get; init; }

    public TransactionStatus? Status { get; init; }

    // 明細のシリアル番号 (完全一致)
    public string? SerialNumber { get; init; }
}
