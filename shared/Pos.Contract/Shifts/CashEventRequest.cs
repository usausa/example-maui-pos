namespace Pos.Contract.Shifts;

// 入出金 (POST /shifts/{id}/cash-events)
public sealed class CashEventRequest
{
    // 端末採番
    public Guid Id { get; set; }

    // NoSale (ドロワ開) は amount = 0
    public CashEventType Type { get; set; }

    [Range(0, double.MaxValue)]
    public decimal Amount { get; set; }

    [MaxLength(100)]
    public string? Reason { get; set; }

    public Guid StaffId { get; set; }

    public DateTime OccurredAt { get; set; }
}
