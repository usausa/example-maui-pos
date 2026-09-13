namespace Pos.Server.Host.Models.Forms;

public sealed class AdjustmentReasonForm
{
    public Guid Id { get; set; }

    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public int SortOrder { get; set; }

    public bool IsActive { get; set; } = true;

    public int Version { get; set; }
}
