namespace Pos.Server.Models.Entity;

[Name("PurchaseOrders")]
public sealed class PurchaseOrderEntity
{
    [Key]
    public Guid Id { get; set; }

    public Guid StoreId { get; set; }

    // 店舗ごとの連番 (登録時に採番)
    public int Seq { get; set; }

    public string PurchaseOrderNo { get; set; } = default!;

    public Guid SupplierId { get; set; }

    public PurchaseOrderStatus Status { get; set; }

    // 希望納期 (入荷予定日になる)
    public DateOnly? ExpectedDate { get; set; }

    public string? Note { get; set; }

    public DateTime? OrderedAt { get; set; }

    // 発注した管理画面のアカウント名
    public string? OrderedBy { get; set; }

    // 発注で作った入荷予定
    public Guid? ReceiptId { get; set; }

    public DateTime? CancelledAt { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public int Version { get; set; }
}
