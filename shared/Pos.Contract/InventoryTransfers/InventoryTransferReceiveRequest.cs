namespace Pos.Contract.InventoryTransfers;

// 受領。明細を省略するか、含めない明細は出荷した数で受け取る (数えた数が違うときだけ送ればよい)
public sealed class InventoryTransferReceiveRequest : IValidatableObject
{
    public Guid? StaffId { get; set; }

    // 省略するとサーバの受付時刻
    public DateTime? ReceivedAt { get; set; }

    [Required]
    public IReadOnlyList<InventoryTransferReceiveRequestLine> Lines { get; set; } = [];

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (Lines.Select(static x => x.LineId).Distinct().Count() != Lines.Count)
        {
            yield return new ValidationResult("明細が重複しています", [nameof(Lines)]);
        }
    }
}

public sealed class InventoryTransferReceiveRequestLine
{
    public Guid LineId { get; set; }

    [Range(0, double.MaxValue)]
    public decimal Quantity { get; set; }
}
