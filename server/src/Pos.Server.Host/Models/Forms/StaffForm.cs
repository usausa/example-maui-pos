namespace Pos.Server.Host.Models.Forms;

public sealed class StaffForm
{
    public Guid Id { get; set; }

    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public StaffRole Role { get; set; } = StaffRole.Cashier;

    // null = 全店舗
    public Guid? StoreId { get; set; }

    public bool IsActive { get; set; } = true;

    public int Version { get; set; }
}
