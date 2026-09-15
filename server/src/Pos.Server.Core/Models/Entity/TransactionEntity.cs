namespace Pos.Server.Models.Entity;

[Name("Transactions")]
public sealed class TransactionEntity
{
    [Key]
    public Guid Id { get; set; }

    public TransactionType Type { get; set; }

    public TransactionStatus Status { get; set; }

    public Guid StoreId { get; set; }

    public Guid TerminalId { get; set; }

    public Guid StaffId { get; set; }

    public Guid ShiftId { get; set; }

    public Guid? CustomerId { get; set; }

    public string ReceiptNo { get; set; } = default!;

    public DateOnly BusinessDate { get; set; }

    public DateTime TransactedAt { get; set; }

    public Guid? OriginalTransactionId { get; set; }

    public decimal Subtotal { get; set; }

    public decimal DiscountTotal { get; set; }

    public decimal NetSubtotal { get; set; }

    public decimal TaxTotal { get; set; }

    public decimal Total { get; set; }

    public decimal TenderedTotal { get; set; }

    public decimal ChangeAmount { get; set; }

    public int PointsEarned { get; set; }

    public int PointsRedeemed { get; set; }

    public int? PointsBalanceAfter { get; set; }

    public string? Note { get; set; }

    public DateTime? VoidedAt { get; set; }

    public Guid? VoidedByStaffId { get; set; }

    public string? VoidReason { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }
}
