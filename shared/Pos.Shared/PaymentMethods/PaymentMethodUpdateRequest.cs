namespace Pos.Shared.PaymentMethods;

public sealed class PaymentMethodUpdateRequest
{
    [Required]
    [MaxLength(20)]
    public string Code { get; set; } = default!;

    [Required]
    [MaxLength(50)]
    public string Name { get; set; } = default!;

    // 端末の支払ボタンに出す短い名前 (省略時は name)
    [MaxLength(10)]
    public string? ShortName { get; set; }

    public PaymentKind Kind { get; set; }

    public bool AllowsChange { get; set; }

    public bool RequiresReference { get; set; }

    public bool IsActive { get; set; }

    public int SortOrder { get; set; }

    public int Version { get; set; }
}
