namespace Pos.Contract.Terminals;

public sealed class TerminalUpdateRequest
{
    public Guid StoreId { get; set; }

    [Range(1, 99)]
    public int TerminalNo { get; set; }

    [Required]
    [MaxLength(Length.TerminalName)]
    public string Name { get; set; } = default!;

    public bool IsActive { get; set; }

    public int Version { get; set; }
}
