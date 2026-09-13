namespace Pos.Shared.Customers;

public sealed class CustomerCreateRequest
{
    [Required]
    [MaxLength(20)]
    public string Code { get; set; } = default!;

    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = default!;

    [MaxLength(100)]
    public string? Kana { get; set; }

    [MaxLength(20)]
    public string? Phone { get; set; }

    [MaxLength(100)]
    public string? Email { get; set; }

    [MaxLength(10)]
    public string? PostalCode { get; set; }

    [MaxLength(200)]
    public string? Address { get; set; }

    public DateOnly? BirthDate { get; set; }

    [MaxLength(500)]
    public string? Note { get; set; }
}
