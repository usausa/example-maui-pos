namespace Pos.Server.Models.Entity;

public sealed class ShiftEntity
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

    // 以下は精算時に確定 (Open 中は取引から都度集計)
    public decimal CashSales { get; set; }

    public decimal CashReturns { get; set; }

    public decimal PaidIn { get; set; }

    public decimal PaidOut { get; set; }

    public int SalesCount { get; set; }

    public int ReturnCount { get; set; }

    public int VoidCount { get; set; }

    public decimal SalesTotal { get; set; }

    public decimal ReturnsTotal { get; set; }

    public string? Note { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }
}
