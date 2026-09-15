namespace Pos.Server.Models.Entity;

[Name("TransactionPayments")]
public sealed class TransactionPaymentEntity
{
    [Key]
    public Guid Id { get; set; }

    public Guid TransactionId { get; set; }

    public int SeqNo { get; set; }

    public Guid PaymentMethodId { get; set; }

    public PaymentKind Kind { get; set; }

    public decimal Amount { get; set; }

    public decimal TenderedAmount { get; set; }

    public string? Reference { get; set; }

    public string? Note { get; set; }
}
