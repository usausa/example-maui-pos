namespace Pos.Terminal.Models.Cart;

public sealed class CartPayment
{
    public Guid Id { get; set; }

    public PaymentMethodListResponseItem Method { get; set; } = default!;

    public decimal Amount { get; set; }

    public decimal TenderedAmount { get; set; }

    public string? Reference { get; set; }
}
