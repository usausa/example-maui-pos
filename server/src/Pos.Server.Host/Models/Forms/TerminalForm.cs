namespace Pos.Server.Host.Models.Forms;

using Pos.Server.Models.Entity;

using Smart.Mapper;

public sealed partial class TerminalForm
{
    public Guid Id { get; set; }

    public Guid? StoreId { get; set; }

    public int TerminalNo { get; set; } = 1;

    public string Name { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    public int Version { get; set; }

    // Entity ↔ フォーム (サーバ付与項目はサービスが設定する)
    [Mapper]
    public static partial TerminalForm ToForm(TerminalEntity entity);

    [Mapper]
    [MapUsing(nameof(TerminalEntity.StoreId), nameof(ResolveStoreId))]
    public static partial TerminalEntity ToEntity(TerminalForm form);

    private static Guid ResolveStoreId(TerminalForm form) => form.StoreId ?? Guid.Empty;
}
