namespace Pos.Server.Host.Models.Forms;

using Pos.Server.Models.Entity;

using Smart.Mapper;

public sealed partial class SupplierForm
{
    public Guid Id { get; set; }

    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string? Phone { get; set; }

    public string? Email { get; set; }

    public string? Note { get; set; }

    public bool IsActive { get; set; } = true;

    public int Version { get; set; }

    // Entity ↔ フォーム (サーバ付与項目はサービスが設定する)
    [Mapper]
    public static partial SupplierForm ToForm(SupplierEntity entity);

    [Mapper]
    public static partial SupplierEntity ToEntity(SupplierForm form);
}
