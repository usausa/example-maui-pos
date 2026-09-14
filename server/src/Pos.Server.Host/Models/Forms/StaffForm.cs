namespace Pos.Server.Host.Models.Forms;

using Pos.Server.Models.Entity;

using Smart.Mapper;

public sealed partial class StaffForm
{
    public Guid Id { get; set; }

    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public StaffRole Role { get; set; } = StaffRole.Cashier;

    // null = 全店舗
    public Guid? StoreId { get; set; }

    public bool IsActive { get; set; } = true;

    public int Version { get; set; }

    // Entity ↔ フォーム (サーバ付与項目はサービスが設定する)
    [Mapper]
    public static partial StaffForm ToForm(StaffEntity entity);

    [Mapper]
    public static partial StaffEntity ToEntity(StaffForm form);
}
