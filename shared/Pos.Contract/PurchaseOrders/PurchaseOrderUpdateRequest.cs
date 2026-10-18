namespace Pos.Contract.PurchaseOrders;

// 発注の変更 (下書きのときだけ)。店舗は変えられない。明細は置き換える
public sealed class PurchaseOrderUpdateRequest : IValidatableObject
{
    public Guid SupplierId { get; set; }

    public DateOnly? ExpectedDate { get; set; }

    [MaxLength(Length.Note)]
    public string? Note { get; set; }

    [Required]
    [MinLength(1)]
    public IReadOnlyList<PurchaseOrderUpdateRequestLine> Lines { get; set; } = default!;

    public int Version { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        // 項目の検証は一覧の null の要素を飛ばすので、要素を読む前に拒む
        if (Lines.Cast<object?>().Contains(null))
        {
            yield return new ValidationResult("明細に null は指定できません", [nameof(Lines)]);
        }
    }
}

public sealed class PurchaseOrderUpdateRequestLine
{
    public Guid ProductId { get; set; }

    [Range(0.01, double.MaxValue)]
    public decimal Quantity { get; set; }

    [Range(0, double.MaxValue)]
    public decimal? Cost { get; set; }
}
