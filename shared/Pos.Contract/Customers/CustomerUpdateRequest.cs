namespace Pos.Contract.Customers;

public sealed class CustomerUpdateRequest
{
    [Required]
    [MaxLength(Length.Code)]
    public string Code { get; set; } = default!;

    [Required]
    [MaxLength(Length.Name)]
    public string Name { get; set; } = default!;

    [MaxLength(Length.Kana)]
    public string? Kana { get; set; }

    [MaxLength(Length.Phone)]
    public string? Phone { get; set; }

    [MaxLength(Length.Email)]
    public string? Email { get; set; }

    [MaxLength(Length.PostalCode)]
    public string? PostalCode { get; set; }

    [MaxLength(Length.Address)]
    public string? Address { get; set; }

    public DateOnly? BirthDate { get; set; }

    [MaxLength(Length.Note)]
    public string? Note { get; set; }

    public int Version { get; set; }
}
