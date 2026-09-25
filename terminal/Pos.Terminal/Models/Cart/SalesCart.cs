namespace Pos.Terminal.Models.Cart;

using System.Text.Json.Serialization;

using Pos.Contract.Customers;

// 会計中の状態。保留は JSON でローカル DB に保存する
[JsonObjectCreationHandling(JsonObjectCreationHandling.Populate)]
public sealed class SalesCart
{
    public Collection<CartLine> Lines { get; } = [];

    // 取引値引
    public Collection<CartDiscount> Discounts { get; } = [];

    public CustomerResponseItem? Customer { get; set; }

    public CartDelivery? Delivery { get; set; }

    public string? Note { get; set; }

    // 受注から会計するとき (会計で受注を完了にする)
    public Guid? OrderId { get; set; }

    public string? OrderNo { get; set; }

    // 受注の前受金 (会計で全額を充てる)
    public decimal DepositAmount { get; set; }

    public bool IsEmpty => Lines.Count == 0;

    public void Clear()
    {
        Lines.Clear();
        Discounts.Clear();
        Customer = null;
        Delivery = null;
        Note = null;
        OrderId = null;
        OrderNo = null;
        DepositAmount = 0m;
    }

    // 同じ商品 (単価変更なし) は数量を足す
    public CartLine Add(ProductResponseItem product, TaxRateResponseItem taxRate, decimal quantity = 1m)
    {
        var existing = Lines.FirstOrDefault(x => (x.Product.Id == product.Id) && (x.UnitPrice == product.Price) && (x.Discounts.Count == 0));
        if (existing is not null)
        {
            existing.Quantity += quantity;
            return existing;
        }

        var line = new CartLine
        {
            Id = Guid.CreateVersion7(),
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
