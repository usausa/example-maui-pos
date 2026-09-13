namespace Pos.Shared.Stores;

public sealed class StoreCreateRequest
{
    [Required]
    [MaxLength(10)]
    public string Code { get; set; } = default!;

    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = default!;

    [MaxLength(10)]
    public string? PostalCode { get; set; }

    [MaxLength(200)]
    public string? Address { get; set; }

    [MaxLength(20)]
    public string? Phone { get; set; }

    [MaxLength(14)]
    public string? RegistrationNo { get; set; }

    [MaxLength(500)]
    public string? ReceiptHeader { get; set; }

    [MaxLength(500)]
    public string? ReceiptFooter { get; set; }

    [Required]
    [MaxLength(50)]
    public string TimeZone { get; set; } = default!;

    public bool IsActive { get; set; } = true;
}
