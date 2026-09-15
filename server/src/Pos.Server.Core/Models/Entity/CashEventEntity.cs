namespace Pos.Server.Models.Entity;

[Name("CashEvents")]
public sealed class CashEventEntity
{
    [Key]
    public Guid Id { get; set; }

    public Guid ShiftId { get; set; }

    public CashEventType Type { get; set; }

    public decimal Amount { get; set; }

    public string? Reason { get; set; }

    public Guid StaffId { get; set; }

    public DateTime OccurredAt { get; set; }

    public DateTime CreatedAt { get; set; }
}
