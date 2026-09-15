namespace Pos.Terminal.Models.Entity;

using Smart.Data.Accessor.Attributes;

// 入出金 (精算時の予想現金の計算に使う)
[Name("CashEvents")]
public sealed class LocalCashEventEntity
{
    [Key]
    public Guid Id { get; set; }

    public Guid ShiftId { get; set; }

    public CashEventType Type { get; set; }

    public decimal Amount { get; set; }

    public string? Reason { get; set; }

    public Guid StaffId { get; set; }

    public DateTime OccurredAt { get; set; }
}
