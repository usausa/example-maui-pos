namespace Pos.Server.Host.Models.Forms;

public sealed class SettingsForm
{
    public string CompanyName { get; set; } = string.Empty;

    public string Currency { get; set; } = "JPY";

    public TaxRounding TaxRounding { get; set; } = TaxRounding.Floor;

    public PointBasis PointBasis { get; set; } = PointBasis.TaxIncluded;

    // HH:mm
    public string BusinessDayStartTime { get; set; } = "05:00";

    public int Version { get; set; }
}
