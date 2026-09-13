namespace Pos.Server.Models.Entity;

// 会社設定 (1 行、Id = 1)
public sealed class SettingsEntity
{
    [Key]
    public int Id { get; set; }

    public string CompanyName { get; set; } = default!;

    public string Currency { get; set; } = default!;

    public TaxRounding TaxRounding { get; set; }

    public PointBasis PointBasis { get; set; }

    public string BusinessDayStartTime { get; set; } = default!;

    public DateTime UpdatedAt { get; set; }

    public int Version { get; set; }
}
