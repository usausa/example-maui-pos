namespace Pos.Terminal.Models.Cart;

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
