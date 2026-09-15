namespace Pos.Contract.Discounts;

public sealed class DiscountUpdateRequest
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

    public bool IsActive { get; set; }

    public int SortOrder { get; set; }

    public int Version { get; set; }
}
