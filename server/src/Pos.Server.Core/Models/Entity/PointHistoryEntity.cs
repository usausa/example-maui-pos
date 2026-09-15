namespace Pos.Server.Models.Entity;

[Name("PointHistories")]
public sealed class PointHistoryEntity
{
    [Key]
    public Guid Id { get; set; }

    public Guid CustomerId { get; set; }

    public PointHistoryType Type { get; set; }

    public int Points { get; set; }

    public int BalanceAfter { get; set; }

    public Guid? TransactionId { get; set; }

    public string? Reason { get; set; }

    public Guid? StaffId { get; set; }

    public DateTime OccurredAt { get; set; }

    public DateTime CreatedAt { get; set; }
}
