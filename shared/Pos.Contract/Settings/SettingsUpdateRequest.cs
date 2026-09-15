namespace Pos.Contract.Settings;

public sealed class SettingsUpdateRequest
{
    [Required]
    [MaxLength(Length.CompanyName)]
    public string CompanyName { get; set; } = default!;

    [Required]
    [MaxLength(Length.Currency)]
    public string Currency { get; set; } = default!;

    public TaxRounding TaxRounding { get; set; }

    public PointBasis PointBasis { get; set; }

    [Required]
    [MaxLength(Length.BusinessDayStartTime)]
    public string BusinessDayStartTime { get; set; } = default!;

    public int Version { get; set; }
}
