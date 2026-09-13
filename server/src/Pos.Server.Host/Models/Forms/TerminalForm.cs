namespace Pos.Server.Host.Models.Forms;

public sealed class TerminalForm
{
    public Guid Id { get; set; }

    public Guid? StoreId { get; set; }

    public int TerminalNo { get; set; } = 1;

    public string Name { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    public int Version { get; set; }
}
