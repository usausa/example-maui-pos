namespace Pos.Server.Models.Entity;

public sealed class TransactionDiscountEntity
{
    [Key]
    public Guid Id { get; set; }

    public Guid TransactionId { get; set; }

    public Guid? LineId { get; set; }

    public Guid? DiscountId { get; set; }

    public int SortNo { get; set; }

    public string Name { get; set; } = default!;

    public DiscountType Type { get; set; }

    public decimal Value { get; set; }

    public decimal Amount { get; set; }

    public string? Reason { get; set; }

    public Guid? ApprovedByStaffId { get; set; }
}
