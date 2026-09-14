namespace Pos.Server.Host.Models.Forms;

using Pos.Server.Models.Entity;

using Smart.Mapper;

public sealed partial class SettingsForm
{
    public string CompanyName { get; set; } = string.Empty;

    public string Currency { get; set; } = "JPY";

    public TaxRounding TaxRounding { get; set; } = TaxRounding.Floor;

    public PointBasis PointBasis { get; set; } = PointBasis.TaxIncluded;

    // HH:mm
    public string BusinessDayStartTime { get; set; } = "05:00";

    public int Version { get; set; }

    // Entity ↔ フォーム (サーバ付与項目はサービスが設定する)
    [Mapper]
    public static partial SettingsForm ToForm(SettingsEntity entity);

    [Mapper]
    public static partial SettingsEntity ToEntity(SettingsForm form);
}
