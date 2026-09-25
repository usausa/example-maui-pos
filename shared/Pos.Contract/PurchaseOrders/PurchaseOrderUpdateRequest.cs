namespace Pos.Contract.PurchaseOrders;

// 発注の変更 (下書きのときだけ)。店舗は変えられない。明細は置き換える
public sealed class PurchaseOrderUpdateRequest
{
    public Guid SupplierId { get; set; }

    public DateOnly? ExpectedDate { get; set; }

    [MaxLength(Length.Note)]
    public string? Note { get; set; }

    [Required]
    [MinLength(1)]
    public IReadOnlyList<PurchaseOrderUpdateRequestLine> Lines { get; set; } = default!;

    public int Version { get; set; }
}

public sealed class PurchaseOrderUpdateRequestLine
{
    public Guid ProductId { get; set; }

    [Range(0.01, double.MaxValue)]
    public decimal Quantity { get; set; }

    [Range(0, double.MaxValue)]
    public decimal? Cost { get; set; }
}
