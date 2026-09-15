namespace Pos.Contract.Discounts;

public sealed class DiscountCreateRequest
{
    [Required]
    [MaxLength(Length.Code)]
    public string Code { get; set; } = default!;

    [Required]
    [MaxLength(Length.DiscountName)]
    public string Name { get; set; } = default!;

    public DiscountType Type { get; set; }

    [Range(0, double.MaxValue)]
    public decimal Value { get; set; }

    public DiscountScope Scope { get; set; }

    public bool RequiresApproval { get; set; }

    public bool IsActive { get; set; } = true;

    public int SortOrder { get; set; }
}
