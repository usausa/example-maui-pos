namespace Pos.Shared.Staff;

public sealed class StaffCreateRequest
{
    [Required]
    [MaxLength(20)]
    public string Code { get; set; } = default!;

    [Required]
    [MaxLength(50)]
    public string Name { get; set; } = default!;

    public StaffRole Role { get; set; }

    public Guid? StoreId { get; set; }

    public bool IsActive { get; set; } = true;
}
