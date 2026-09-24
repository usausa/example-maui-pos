namespace Pos.Server.Models.Entity;

[Name("Orders")]
public sealed class OrderEntity
{
    [Key]
    public Guid Id { get; set; }

    public Guid StoreId { get; set; }

    // 店舗ごとの連番 (登録時に採番)
    public int Seq { get; set; }

    public string OrderNo { get; set; } = default!;

    public Guid? TerminalId { get; set; }

    public Guid StaffId { get; set; }

    public Guid? CustomerId { get; set; }

    public string CustomerName { get; set; } = default!;

    public string? Phone { get; set; }

    public OrderType Type { get; set; }

    public OrderStatus Status { get; set; }

    public DateOnly? RequestedDate { get; set; }

    public string? Note { get; set; }

    // 明細の金額の合計
    public decimal Total { get; set; }

    public Guid? TransactionId { get; set; }

    public DateTime OrderedAt { get; set; }

    public DateTime? ArrivedAt { get; set; }

    public DateTime? CompletedAt { get; set; }

    public DateTime? CancelledAt { get; set; }

    public string? CancelReason { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public int Version { get; set; }
}
