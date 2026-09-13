namespace Pos.Server.Host.Models.Forms;

// ポイント手動調整 (S-62)
public sealed class PointAdjustForm
{
    public int Points { get; set; }

    public string Reason { get; set; } = string.Empty;

    public Guid? StaffId { get; set; }
}
