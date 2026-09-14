namespace Pos.Server.Host.Models.Forms;

using Pos.Server.Models.Entity;

using Smart.Mapper;

public sealed partial class StoreForm
{
    public Guid Id { get; set; }

    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string? PostalCode { get; set; }

    public string? Address { get; set; }

    public string? Phone { get; set; }

    public string? RegistrationNo { get; set; }

    public string? ReceiptHeader { get; set; }

    public string? ReceiptFooter { get; set; }

    public string TimeZone { get; set; } = "Asia/Tokyo";

    public bool IsActive { get; set; } = true;

    public int Version { get; set; }

    // Entity ↔ フォーム (サーバ付与項目はサービスが設定する)
    [Mapper]
    public static partial StoreForm ToForm(StoreEntity entity);

    [Mapper]
    public static partial StoreEntity ToEntity(StoreForm form);
}
