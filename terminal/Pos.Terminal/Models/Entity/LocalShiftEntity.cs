namespace Pos.Terminal.Models.Entity;

// 端末で開設したシフト
public sealed class LocalShiftEntity
{
    [Key]
    public Guid Id { get; set; }

    public Guid StoreId { get; set; }

    public Guid TerminalId { get; set; }

    public ShiftStatus Status { get; set; }

    public DateOnly BusinessDate { get; set; }

    public DateTime OpenedAt { get; set; }

    public Guid OpenedByStaffId { get; set; }

    public decimal OpeningCash { get; set; }

    public DateTime? ClosedAt { get; set; }

    public Guid? ClosedByStaffId { get; set; }

    public decimal? ActualCash { get; set; }

    public decimal? ExpectedCash { get; set; }

    public decimal? Difference { get; set; }

    public string? Note { get; set; }
}
