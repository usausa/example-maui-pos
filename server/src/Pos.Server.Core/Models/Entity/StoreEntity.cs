namespace Pos.Server.Models.Entity;

[Name("Stores")]
public sealed class StoreEntity
{
    [Key]
    public Guid Id { get; set; }

    public string Code { get; set; } = default!;

    public string Name { get; set; } = default!;

    public string? PostalCode { get; set; }

    public string? Address { get; set; }

    public string? Phone { get; set; }

    public string? RegistrationNo { get; set; }

    public string? ReceiptHeader { get; set; }

    public string? ReceiptFooter { get; set; }

    public string TimeZone { get; set; } = default!;

    public bool IsActive { get; set; }

    public bool IsDeleted { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public int Version { get; set; }
}
