namespace Pos.Contract.PaymentMethods;

public sealed class PaymentMethodUpdateRequest
{
    [Required]
    [MaxLength(Length.Code)]
    public string Code { get; set; } = default!;

    [Required]
    [MaxLength(Length.PaymentMethodName)]
    public string Name { get; set; } = default!;

    // 端末の支払ボタンに出す短い名前 (省略時は name)
    [MaxLength(Length.PaymentMethodShortName)]
    public string? ShortName { get; set; }

    public PaymentKind Kind { get; set; }

    public bool AllowsChange { get; set; }

    public bool RequiresReference { get; set; }

    public bool IsActive { get; set; }

    public int SortOrder { get; set; }

    public int Version { get; set; }
}
