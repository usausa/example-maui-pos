namespace Pos.Shared.Settings;

public sealed class SettingsResponse
{
    public string CompanyName { get; set; } = default!;

    public string Currency { get; set; } = default!;

    public TaxRounding TaxRounding { get; set; }

    public PointBasis PointBasis { get; set; }

    // "05:00"。この時刻より前は前営業日
    public string BusinessDayStartTime { get; set; } = default!;

    public DateTime UpdatedAt { get; set; }

    public int Version { get; set; }
}
