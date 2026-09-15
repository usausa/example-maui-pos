namespace Pos.Contract.Staff;

public sealed class StaffCreateRequest
{
    [Required]
    [MaxLength(Length.Code)]
    public string Code { get; set; } = default!;

    [Required]
    [MaxLength(Length.StaffName)]
    public string Name { get; set; } = default!;

    public StaffRole Role { get; set; }

    public Guid? StoreId { get; set; }

    public bool IsActive { get; set; } = true;
}
