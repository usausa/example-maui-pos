namespace Pos.Contract.InventoryReceipts;

// 入荷予定の登録 (管理画面)。受領で在庫に入る
public sealed class InventoryReceiptCreateRequest
{
    public Guid StoreId { get; set; }

    public Guid SupplierId { get; set; }

    [MaxLength(Length.SlipNo)]
    public string? SlipNo { get; set; }

    public DateOnly? ExpectedDate { get; set; }

    [MaxLength(Length.Note)]
    public string? Note { get; set; }

    [Required]
    [MinLength(1)]
    public IReadOnlyList<InventoryReceiptCreateRequestLine> Lines { get; set; } = default!;
}

public sealed class InventoryReceiptCreateRequestLine
{
    public Guid ProductId { get; set; }

    [Range(0.01, double.MaxValue)]
    public decimal Quantity { get; set; }

    // 仕入単価
    [Range(0, double.MaxValue)]
    public decimal? Cost { get; set; }
}
