namespace Pos.Server.Host.Models.Forms;

public sealed class StoreForm
{
    public Guid Id { get; set; }

    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string? PostalCode { get; set; }

    public string? Address { get; set; }

    public string? Phone { get; set; }

    public string? RegistrationNo { get; set; }

    public string? ReceiptHeader { get; set; }

    public string? ReceiptFooter { get; set; }

    public string TimeZone { get; set; } = "Asia/Tokyo";

    public bool IsActive { get; set; } = true;

    public int Version { get; set; }
}
