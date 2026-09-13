namespace Pos.Shared.PaymentMethods;

public sealed class PaymentMethodCreateRequest
{
    [Required]
    [MaxLength(20)]
    public string Code { get; set; } = default!;

    [Required]
    [MaxLength(50)]
    public string Name { get; set; } = default!;

    public PaymentKind Kind { get; set; }

    public bool AllowsChange { get; set; }

    public bool RequiresReference { get; set; }

    public bool IsActive { get; set; } = true;

    public int SortOrder { get; set; }
}
