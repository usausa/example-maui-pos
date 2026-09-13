namespace Pos.Shared.Transactions;

public sealed class TransactionVoidRequest
{
    public Guid StaffId { get; set; }

    [Required]
    [MaxLength(200)]
    public string Reason { get; set; } = default!;

    public DateTime VoidedAt { get; set; }
}
