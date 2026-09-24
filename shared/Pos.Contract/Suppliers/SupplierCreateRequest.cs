namespace Pos.Contract.Suppliers;

public sealed class SupplierCreateRequest
{
    [Required]
    [MaxLength(Length.Code)]
    public string Code { get; set; } = default!;

    [Required]
    [MaxLength(Length.SupplierName)]
    public string Name { get; set; } = default!;

    [MaxLength(Length.Phone)]
    public string? Phone { get; set; }

    [MaxLength(Length.Email)]
    public string? Email { get; set; }

    [MaxLength(Length.Note)]
    public string? Note { get; set; }

    public bool IsActive { get; set; } = true;
}
