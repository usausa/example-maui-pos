namespace Pos.Server.Host.Models.Forms;

using Pos.Server.Models.Entity;

using Smart.Mapper;

public sealed partial class TaxRateForm
{
    public Guid Id { get; set; }

    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    // 0.10 = 10%
    public decimal Rate { get; set; }

    public TaxKind Kind { get; set; } = TaxKind.Standard;

    public bool IsDefault { get; set; }

    public int SortOrder { get; set; }

    public int Version { get; set; }

    // Entity ↔ フォーム (サーバ付与項目はサービスが設定する)
    [Mapper]
    public static partial TaxRateForm ToForm(TaxRateEntity entity);

    [Mapper]
    public static partial TaxRateEntity ToEntity(TaxRateForm form);
}
