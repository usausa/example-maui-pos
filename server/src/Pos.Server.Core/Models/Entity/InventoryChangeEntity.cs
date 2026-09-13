namespace Pos.Server.Models.Entity;

public sealed class InventoryChangeEntity
{
    [Key]
    public Guid Id { get; set; }

    public Guid StoreId { get; set; }

    public Guid ProductId { get; set; }

    public InventoryChangeType Type { get; set; }

    public decimal QuantityDelta { get; set; }

    public decimal QuantityAfter { get; set; }

    public Guid? ReasonId { get; set; }

    public string? Reason { get; set; }

    public string? ReferenceType { get; set; }

    public Guid? ReferenceId { get; set; }

    public Guid? ReferenceLineId { get; set; }

    public Guid? StaffId { get; set; }

    public DateTime OccurredAt { get; set; }

    public DateTime CreatedAt { get; set; }
}
