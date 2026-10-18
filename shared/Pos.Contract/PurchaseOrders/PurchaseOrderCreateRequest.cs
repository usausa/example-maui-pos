namespace Pos.Contract.PurchaseOrders;

// 発注の登録 (管理画面)。下書きで登録し、[発注] で入荷予定を作る
public sealed class PurchaseOrderCreateRequest : IValidatableObject
{
    public Guid StoreId { get; set; }

    public Guid SupplierId { get; set; }

    // 希望納期 (作る入荷予定の入荷予定日)
    public DateOnly? ExpectedDate { get; set; }

    [MaxLength(Length.Note)]
    public string? Note { get; set; }

    [Required]
    [MinLength(1)]
    public IReadOnlyList<PurchaseOrderCreateRequestLine> Lines { get; set; } = default!;

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        // 項目の検証は一覧の null の要素を飛ばすので、要素を読む前に拒む
        if (Lines.Cast<object?>().Contains(null))
        {
            yield return new ValidationResult("明細に null は指定できません", [nameof(Lines)]);
        }
    }
}

public sealed class PurchaseOrderCreateRequestLine
{
    public Guid ProductId { get; set; }

    [Range(0.01, double.MaxValue)]
    public decimal Quantity { get; set; }

    // 仕入単価
    [Range(0, double.MaxValue)]
    public decimal? Cost { get; set; }
}
