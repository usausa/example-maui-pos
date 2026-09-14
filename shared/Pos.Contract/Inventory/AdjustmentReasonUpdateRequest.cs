namespace Pos.Contract.Inventory;

public sealed class AdjustmentReasonUpdateRequest
{
    [Required]
    [MaxLength(20)]
    public string Code { get; set; } = default!;

    [Required]
    [MaxLength(50)]
    public string Name { get; set; } = default!;

    public int SortOrder { get; set; }

    public bool IsActive { get; set; }

    public int Version { get; set; }
}
