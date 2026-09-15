namespace Pos.Contract.TaxRates;

public sealed class TaxRateCreateRequest
{
    [Required]
    [MaxLength(Length.TaxRateCode)]
    public string Code { get; set; } = default!;

    [Required]
    [MaxLength(Length.TaxRateName)]
    public string Name { get; set; } = default!;

    [Range(0, 1)]
    public decimal Rate { get; set; }

    public TaxKind Kind { get; set; }

    public bool IsDefault { get; set; }

    public int SortOrder { get; set; }
}
