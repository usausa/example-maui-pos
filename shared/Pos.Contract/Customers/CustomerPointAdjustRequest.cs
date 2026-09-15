namespace Pos.Contract.Customers;

// 手動調整 (Adjust 履歴を作る)
public sealed class CustomerPointAdjustRequest
{
    public int Points { get; set; }

    [Required]
    [MaxLength(Length.Reason)]
    public string Reason { get; set; } = default!;

    public Guid StaffId { get; set; }
}
