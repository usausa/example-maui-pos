namespace Pos.Contract.Discounts;

public sealed class DiscountCreateRequest
{
    [Required]
    [MaxLength(20)]
    public string Code { get; set; } = default!;

    [Required]
    [MaxLength(50)]
    public string Name { get; set; } = default!;

    public DiscountType Type { get; set; }

    [Range(0, double.MaxValue)]
    public decimal Value { get; set; }

    public DiscountScope Scope { get; set; }

    public bool RequiresApproval { get; set; }

    public bool IsActive { get; set; } = true;

    public int SortOrder { get; set; }
}
