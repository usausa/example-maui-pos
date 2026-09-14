namespace Pos.Contract.Customers;

using Pos.Contract;

public sealed class PointHistoryResponseItem
{
    public Guid Id { get; set; }

    public Guid CustomerId { get; set; }

    public PointHistoryType Type { get; set; }

    // 符号付き増減
    public int Points { get; set; }

    public int BalanceAfter { get; set; }

    public Guid? TransactionId { get; set; }

    public string? Reason { get; set; }

    public Guid? StaffId { get; set; }

    public DateTime OccurredAt { get; set; }
}

public sealed class PointHistoryResponse : ListResponse<PointHistoryResponseItem>;
