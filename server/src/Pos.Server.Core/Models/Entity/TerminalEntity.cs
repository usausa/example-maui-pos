namespace Pos.Server.Models.Entity;

[Name("Terminals")]
public sealed class TerminalEntity
{
    [Key]
    public Guid Id { get; set; }

    public Guid StoreId { get; set; }

    public int TerminalNo { get; set; }

    public string Name { get; set; } = default!;

    public int LastReceiptSeq { get; set; }

    public DateTime? LastSeenAt { get; set; }

    public string? AppVersion { get; set; }

    public bool IsActive { get; set; }

    public bool IsDeleted { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public int Version { get; set; }
}
