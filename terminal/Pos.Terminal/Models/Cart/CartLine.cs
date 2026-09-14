namespace Pos.Terminal.Models.Cart;

using System.Text.Json.Serialization;

[JsonObjectCreationHandling(JsonObjectCreationHandling.Populate)]
public sealed class CartLine
{
    public Guid Id { get; set; }

    public ProductResponseItem Product { get; set; } = default!;

    public TaxRateResponseItem TaxRate { get; set; } = default!;

    public decimal UnitPrice { get; set; }

    public decimal Quantity { get; set; }

    // 明細値引
    public Collection<CartDiscount> Discounts { get; } = [];

    public Collection<string> SerialNumbers { get; } = [];

    public string? Note { get; set; }
}
