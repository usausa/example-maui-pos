namespace Pos.Terminal.Models.Entity;

// 端末で発生した取引 (履歴・再印字・返品の元取引参照)。Payload は TransactionResponseItem の JSON (送信後はサーバの応答で置き換える)
public sealed class LocalTransactionEntity
{
    [Key]
    public Guid Id { get; set; }

    public TransactionType Type { get; set; }

    public TransactionStatus Status { get; set; }

    public Guid ShiftId { get; set; }

    public string ReceiptNo { get; set; } = default!;

    public DateOnly BusinessDate { get; set; }

    public DateTime TransactedAt { get; set; }

    public Guid? CustomerId { get; set; }

    public decimal Total { get; set; }

    public int PointsEarned { get; set; }

    public int PointsRedeemed { get; set; }

    public Guid? OriginalTransactionId { get; set; }

    public string Payload { get; set; } = default!;
}
