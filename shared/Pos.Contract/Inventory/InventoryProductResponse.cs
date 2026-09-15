namespace Pos.Contract.Inventory;

public sealed class InventoryProductResponse
{
    public Guid ProductId { get; set; }

    public IReadOnlyList<InventoryProductResponseLevel> Levels { get; set; } = default!;
}

public sealed class InventoryProductResponseLevel
{
    public Guid StoreId { get; set; }

    public string StoreName { get; set; } = default!;

    public decimal Quantity { get; set; }

    public DateTime UpdatedAt { get; set; }
}
