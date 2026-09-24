namespace Pos.Contract.Inventory;

// 棚卸・調整の一括登録 (POST /inventory/changes)
public sealed class InventoryChangeRequest
{
    [Required]
    public IReadOnlyList<InventoryChangeRequestChange> Changes { get; set; } = default!;
}

public sealed class InventoryChangeRequestChange : IValidatableObject
{
    // 端末採番
    public Guid Id { get; set; }

    public Guid StoreId { get; set; }

    public Guid ProductId { get; set; }

    // PhysicalCount (実数) / Adjustment (増減)
    public InventoryChangeType Type { get; set; }

    // PhysicalCount は絶対数量、Adjustment は符号付き増減
    public decimal Quantity { get; set; }

    public Guid? ReasonId { get; set; }

    [MaxLength(Length.Reason)]
    public string? Reason { get; set; }

    public Guid StaffId { get; set; }

    public DateTime OccurredAt { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (!Type.IsManual())
        {
            yield return new ValidationResult("種別は PhysicalCount か Adjustment を指定してください", [nameof(Type)]);
        }
    }
}

// 要素ごとの結果
public sealed class InventoryChangeResultResponse
{
    public IReadOnlyList<InventoryChangeResultResponseResult> Results { get; set; } = default!;
}

public sealed class InventoryChangeResultResponseResult
{
    public Guid Id { get; set; }

    public InventoryChangeResultStatus Status { get; set; }

    public decimal QuantityDelta { get; set; }

    public decimal QuantityAfter { get; set; }
}

public enum InventoryChangeResultStatus
{
    Created,
    Duplicate
}
