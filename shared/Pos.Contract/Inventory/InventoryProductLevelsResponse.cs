namespace Pos.Contract.Inventory;

public sealed class InventoryProductLevelsResponse
{
    public Guid ProductId { get; set; }

    public IReadOnlyList<InventoryProductLevelsResponseLevel> Levels { get; set; } = default!;
}

public sealed class InventoryProductLevelsResponseLevel
{
    public Guid StoreId { get; set; }

    public string StoreName { get; set; } = default!;

    public decimal Quantity { get; set; }

    public DateTime UpdatedAt { get; set; }
}
