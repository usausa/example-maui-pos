namespace Pos.Server.Host.Models.Forms;

public sealed class DiscountForm
{
    public Guid Id { get; set; }

    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public DiscountType Type { get; set; } = DiscountType.Amount;

    // Amount は金額、Percent は率 (0.05 = 5%)
    public decimal Value { get; set; }

    public DiscountScope Scope { get; set; } = DiscountScope.Line;

    public bool RequiresApproval { get; set; }

    public bool IsActive { get; set; } = true;

    public int SortOrder { get; set; }

    public int Version { get; set; }
}
