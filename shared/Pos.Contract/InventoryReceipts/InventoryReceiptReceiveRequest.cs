namespace Pos.Contract.InventoryReceipts;

// 受領。明細を省略するか、含めない明細は予定の数で受け取る (数えた数が違うときだけ送ればよい)
public sealed class InventoryReceiptReceiveRequest : IValidatableObject
{
    public Guid? StaffId { get; set; }

    // 省略するとサーバの受付時刻
    public DateTime? ReceivedAt { get; set; }

    [Required]
    public IReadOnlyList<InventoryReceiptReceiveRequestLine> Lines { get; set; } = [];

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        // 項目の検証は一覧の null の要素を飛ばすので、要素を読む前に拒む
        if (Lines.Cast<object?>().Contains(null))
        {
            yield return new ValidationResult("明細に null は指定できません", [nameof(Lines)]);
            yield break;
        }

        if (Lines.Select(static x => x.LineId).Distinct().Count() != Lines.Count)
        {
            yield return new ValidationResult("明細が重複しています", [nameof(Lines)]);
        }
    }
}

public sealed class InventoryReceiptReceiveRequestLine
{
    public Guid LineId { get; set; }

    [Range(0, double.MaxValue)]
    public decimal Quantity { get; set; }
}
