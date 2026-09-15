namespace Pos.Contract.AdjustmentReasons;

public sealed class AdjustmentReasonUpdateRequest
{
    [Required]
    [MaxLength(Length.Code)]
    public string Code { get; set; } = default!;

    [Required]
    [MaxLength(Length.AdjustmentReasonName)]
    public string Name { get; set; } = default!;

    public int SortOrder { get; set; }

    public bool IsActive { get; set; }

    public int Version { get; set; }
}
