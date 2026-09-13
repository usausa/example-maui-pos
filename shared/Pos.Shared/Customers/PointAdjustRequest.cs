namespace Pos.Shared.Customers;

// 手動調整 (Adjust 履歴を作る)
public sealed class PointAdjustRequest
{
    public int Points { get; set; }

    [Required]
    [MaxLength(200)]
    public string Reason { get; set; } = default!;

    public Guid StaffId { get; set; }
}
