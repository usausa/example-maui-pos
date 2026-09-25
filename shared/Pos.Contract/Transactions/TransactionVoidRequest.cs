namespace Pos.Contract.Transactions;

public sealed class TransactionVoidRequest
{
    public Guid StaffId { get; set; }

    // 担当がレジ係のときは店長以上の承認者が要る
    public Guid? ApprovedByStaffId { get; set; }

    [Required]
    [MaxLength(Length.Reason)]
    public string Reason { get; set; } = default!;

    public DateTime VoidedAt { get; set; }
}
