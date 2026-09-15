namespace Pos.Server.Models.Entity;

[Name("InventoryLevels")]
public sealed class InventoryLevelEntity
{
    [Key]
    public Guid StoreId { get; set; }

    [Key]
    public Guid ProductId { get; set; }

    public decimal Quantity { get; set; }

    public DateTime UpdatedAt { get; set; }
}
