namespace Pos.Shared.Shifts;

// 開設 (POST /shifts)。端末に Open のシフトがあれば 409
public sealed class ShiftOpenRequest
{
    // 端末採番
    public Guid Id { get; set; }

    public Guid StoreId { get; set; }

    public Guid TerminalId { get; set; }

    public DateOnly BusinessDate { get; set; }

    public DateTime OpenedAt { get; set; }

    public Guid OpenedByStaffId { get; set; }

    [Range(0, double.MaxValue)]
    public decimal OpeningCash { get; set; }

    [MaxLength(500)]
    public string? Note { get; set; }
}
