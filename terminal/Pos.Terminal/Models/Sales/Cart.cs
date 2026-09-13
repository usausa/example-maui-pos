namespace Pos.Terminal.Models.Sales;

using System.Text.Json.Serialization;

using Pos.Shared.Customers;

// 会計中の状態 (T-10 〜 T-20)。保留は JSON でローカル DB に保存する
[JsonObjectCreationHandling(JsonObjectCreationHandling.Populate)]
public sealed class Cart
{
    public Collection<CartLine> Lines { get; } = [];

    // 取引値引
    public Collection<CartDiscount> Discounts { get; } = [];

    public CustomerResponse? Customer { get; set; }

    public CartDelivery? Delivery { get; set; }

    public string? Note { get; set; }

    public bool IsEmpty => Lines.Count == 0;

    public void Clear()
    {
        Lines.Clear();
        Discounts.Clear();
        Customer = null;
        Delivery = null;
        Note = null;
    }

    // 同じ商品 (単価変更なし) は数量を足す
    public CartLine Add(ProductResponse product, TaxRateResponse taxRate, decimal quantity = 1m)
    {
        var existing = Lines.FirstOrDefault(x => (x.Product.Id == product.Id) && (x.UnitPrice == product.Price) && (x.Discounts.Count == 0));
        if (existing is not null)
        {
            existing.Quantity += quantity;
            return existing;
        }

        var line = new CartLine
        {
            Id = Guid.NewGuid(),
            Product = product,
            TaxRate = taxRate,
            UnitPrice = product.Price,
            Quantity = quantity
        };
        Lines.Add(line);
        return line;
    }

    public string Summary => Lines.Count == 0 ? string.Empty : $"{Lines[0].Product.Name}{(Lines.Count > 1 ? $" 他 {Lines.Count - 1} 点" : string.Empty)}";
}

[JsonObjectCreationHandling(JsonObjectCreationHandling.Populate)]
public sealed class CartLine
{
    public Guid Id { get; set; }

    public ProductResponse Product { get; set; } = default!;

    public TaxRateResponse TaxRate { get; set; } = default!;

    public decimal UnitPrice { get; set; }

    public decimal Quantity { get; set; }

    // 明細値引
    public Collection<CartDiscount> Discounts { get; } = [];

    public Collection<string> SerialNumbers { get; } = [];

    public string? Note { get; set; }
}

public sealed class CartDiscount
{
    public Guid Id { get; set; }

    // 定義済み値引。null = 任意値引
    public Guid? DiscountId { get; set; }

    public string Name { get; set; } = default!;

    public DiscountType Type { get; set; }

    public decimal Value { get; set; }

    public string? Reason { get; set; }

    public Guid? ApprovedByStaffId { get; set; }
}

public sealed class CartPayment
{
    public Guid Id { get; set; }

    public PaymentMethodResponse Method { get; set; } = default!;

    public decimal Amount { get; set; }

    public decimal TenderedAmount { get; set; }

    public string? Reference { get; set; }
}

public sealed class CartDelivery
{
    public string RecipientName { get; set; } = string.Empty;

    public string? Phone { get; set; }

    public string? PostalCode { get; set; }

    public string Address { get; set; } = string.Empty;

    public DateOnly? RequestedDate { get; set; }

    public string? TimeSlot { get; set; }

    public string? Note { get; set; }
}
