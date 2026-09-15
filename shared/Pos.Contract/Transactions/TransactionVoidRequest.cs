namespace Pos.Contract.Transactions;

public sealed class TransactionVoidRequest
{
    public Guid StaffId { get; set; }

    [Required]
    [MaxLength(Length.Reason)]
    public string Reason { get; set; } = default!;

    public DateTime VoidedAt { get; set; }
}
