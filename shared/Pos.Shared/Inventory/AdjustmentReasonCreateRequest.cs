namespace Pos.Shared.Inventory;

public sealed class AdjustmentReasonCreateRequest
{
    [Required]
    [MaxLength(20)]
    public string Code { get; set; } = default!;

    [Required]
    [MaxLength(50)]
    public string Name { get; set; } = default!;

    public int SortOrder { get; set; }

    public bool IsActive { get; set; } = true;
}
