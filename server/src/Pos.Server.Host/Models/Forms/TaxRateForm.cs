namespace Pos.Server.Host.Models.Forms;

public sealed class TaxRateForm
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
}
