namespace Pos.Contract.Terminals;

public sealed class TerminalCreateRequest
{
    public Guid StoreId { get; set; }

    [Range(1, 99)]
    public int TerminalNo { get; set; }

    [Required]
    [MaxLength(Length.TerminalName)]
    public string Name { get; set; } = default!;

    public bool IsActive { get; set; } = true;
}
