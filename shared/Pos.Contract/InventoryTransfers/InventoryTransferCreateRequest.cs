namespace Pos.Contract.InventoryTransfers;

// 店舗間移動の依頼 (管理画面)。出荷で出荷店の在庫が減り、受領で入荷店の在庫が増える
public sealed class InventoryTransferCreateRequest : IValidatableObject
{
    public Guid FromStoreId { get; set; }

    public Guid ToStoreId { get; set; }

    [MaxLength(Length.Note)]
    public string? Note { get; set; }

    [Required]
    [MinLength(1)]
    public IReadOnlyList<InventoryTransferCreateRequestLine> Lines { get; set; } = default!;

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (FromStoreId == ToStoreId)
        {
            yield return new ValidationResult("出荷店と入荷店を別の店舗にしてください", [nameof(ToStoreId)]);
        }
    }
}

public sealed class InventoryTransferCreateRequestLine
{
    public Guid ProductId { get; set; }

    [Range(0.01, double.MaxValue)]
    public decimal Quantity { get; set; }
}
