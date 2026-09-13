namespace Pos.Shared.TaxRates;

public sealed class TaxRateUpdateRequest
{
    [Required]
    [MaxLength(10)]
    public string Code { get; set; } = default!;

    [Required]
    [MaxLength(50)]
    public string Name { get; set; } = default!;

    [Range(0, 1)]
    public decimal Rate { get; set; }

    public TaxKind Kind { get; set; }

    public bool IsDefault { get; set; }

    public int SortOrder { get; set; }

    public int Version { get; set; }
}
