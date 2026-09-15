namespace Pos.Server.Models.Entity;

[Name("TransactionDeliveries")]
public sealed class TransactionDeliveryEntity
{
    [Key]
    public Guid TransactionId { get; set; }

    public string RecipientName { get; set; } = default!;

    public string? Phone { get; set; }

    public string? PostalCode { get; set; }

    public string Address { get; set; } = default!;

    public DateOnly? RequestedDate { get; set; }

    public string? TimeSlot { get; set; }

    public string? Note { get; set; }
}
