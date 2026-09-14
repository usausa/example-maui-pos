namespace Pos.Server.Host.Models.Forms;

using Pos.Server.Models.Entity;

using Smart.Mapper;

public sealed partial class PaymentMethodForm
{
    public Guid Id { get; set; }

    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string? ShortName { get; set; }

    public PaymentKind Kind { get; set; } = PaymentKind.Cash;

    public bool AllowsChange { get; set; }

    public bool RequiresReference { get; set; }

    public bool IsActive { get; set; } = true;

    public int SortOrder { get; set; }

    public int Version { get; set; }

    // Entity ↔ フォーム (サーバ付与項目はサービスが設定する)
    [Mapper]
    public static partial PaymentMethodForm ToForm(PaymentMethodEntity entity);

    [Mapper]
    public static partial PaymentMethodEntity ToEntity(PaymentMethodForm form);
}
