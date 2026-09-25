namespace Pos.Contract.Orders;

// 前受金の返金 (POST /orders/{id}/deposit/refund。端末)。前受金の全額を、受け取った方法で返す
public sealed class OrderDepositRefundRequest
{
    public Guid Id { get; set; }

    public Guid ShiftId { get; set; }

    public Guid TerminalId { get; set; }

    public Guid StaffId { get; set; }

    public DateTime? OccurredAt { get; set; }
}
