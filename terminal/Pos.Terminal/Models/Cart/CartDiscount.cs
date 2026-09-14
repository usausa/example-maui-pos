namespace Pos.Terminal.Models.Cart;

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
