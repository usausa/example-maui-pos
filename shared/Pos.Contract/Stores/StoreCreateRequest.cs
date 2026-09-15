namespace Pos.Contract.Stores;

public sealed class StoreCreateRequest
{
    [Required]
    [MaxLength(Length.StoreCode)]
    public string Code { get; set; } = default!;

    [Required]
    [MaxLength(Length.Name)]
    public string Name { get; set; } = default!;

    [MaxLength(Length.PostalCode)]
    public string? PostalCode { get; set; }

    [MaxLength(Length.Address)]
    public string? Address { get; set; }

    [MaxLength(Length.Phone)]
    public string? Phone { get; set; }

    [MaxLength(Length.RegistrationNo)]
    public string? RegistrationNo { get; set; }

    [MaxLength(Length.ReceiptText)]
    public string? ReceiptHeader { get; set; }

    [MaxLength(Length.ReceiptText)]
    public string? ReceiptFooter { get; set; }

    [Required]
    [MaxLength(Length.TimeZone)]
    public string TimeZone { get; set; } = default!;

    public bool IsActive { get; set; } = true;
}
