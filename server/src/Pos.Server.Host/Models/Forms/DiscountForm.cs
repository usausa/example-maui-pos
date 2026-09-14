namespace Pos.Server.Host.Models.Forms;

using Pos.Server.Models.Entity;

using Smart.Mapper;

public sealed partial class DiscountForm
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

    // Entity ↔ フォーム (サーバ付与項目はサービスが設定する)
    [Mapper]
    public static partial DiscountForm ToForm(DiscountEntity entity);

    [Mapper]
    public static partial DiscountEntity ToEntity(DiscountForm form);
}
