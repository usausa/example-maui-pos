namespace Pos.Shared.Settings;

public sealed class SettingsUpdateRequest
{
    [Required]
    [MaxLength(100)]
    public string CompanyName { get; set; } = default!;

    [Required]
    [MaxLength(3)]
    public string Currency { get; set; } = default!;

    public TaxRounding TaxRounding { get; set; }

    public PointBasis PointBasis { get; set; }

    [Required]
    [MaxLength(5)]
    public string BusinessDayStartTime { get; set; } = default!;

    public int Version { get; set; }
}
