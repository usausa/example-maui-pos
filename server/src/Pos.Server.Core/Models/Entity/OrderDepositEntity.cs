namespace Pos.Server.Models.Entity;

[Name("OrderDeposits")]
public sealed class OrderDepositEntity
{
    // 端末が採番 (同じ Id の再送は受け取り済みとして扱う)
    [Key]
    public Guid Id { get; set; }

    public Guid OrderId { get; set; }

    public Guid StoreId { get; set; }

    public Guid TerminalId { get; set; }

    public Guid ShiftId { get; set; }

    public Guid StaffId { get; set; }

    public OrderDepositType Type { get; set; }

    public Guid PaymentMethodId { get; set; }

    public PaymentKind Kind { get; set; }

    public decimal Amount { get; set; }

    // カードの伝票番号など
    public string? Reference { get; set; }

    public DateTime OccurredAt { get; set; }

    public DateTime CreatedAt { get; set; }
}
