namespace Pos.Contract.Orders;

// 前受金の受取 (POST /orders/{id}/deposit。端末)。未完了の受注で、前受金がないときだけ。Id は端末が採番し、同じ Id の再送は受け取り済みとして扱う
public sealed class OrderDepositRequest
{
    public Guid Id { get; set; }

    public Guid ShiftId { get; set; }

    public Guid TerminalId { get; set; }

    public Guid StaffId { get; set; }

    public Guid PaymentMethodId { get; set; }

    [Range(1, double.MaxValue)]
    public decimal Amount { get; set; }

    [MaxLength(Length.Reference)]
    public string? Reference { get; set; }

    // 省略するとサーバの受付時刻
    public DateTime? OccurredAt { get; set; }
}
