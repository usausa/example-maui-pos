namespace Pos.Terminal.Models.Entity;

using Smart.Data.Accessor.Attributes;

// 送信待ちの種類 (送信順は発生順。シフト開設 → 取引 / 入出金 → 精算)
public enum OutboxKind
{
    ShiftOpen,
    Transaction,
    TransactionVoid,
    CashEvent,
    ShiftClose,
    InventoryChanges
}

// Failed は 409 / 422 の「要確認」(後続を送らない)
public enum OutboxStatus
{
    Pending,
    Sent,
    Failed
}

// 送信待ち。Payload は XxxRequest の JSON
[Name("Outbox")]
public sealed class OutboxEntity
{
    [Key]
    public Guid Id { get; set; }

    public OutboxKind Kind { get; set; }

    // 対象 (取引 ID / シフト ID)
    public Guid TargetId { get; set; }

    public string Payload { get; set; } = default!;

    public DateTime CreatedAt { get; set; }

    public OutboxStatus Status { get; set; }

    public int Attempts { get; set; }

    public string? LastError { get; set; }

    public DateTime? SentAt { get; set; }
}
