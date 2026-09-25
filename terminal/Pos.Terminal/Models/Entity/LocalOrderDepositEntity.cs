namespace Pos.Terminal.Models.Entity;

using Smart.Data.Accessor.Attributes;

// 受注の前受金 (サーバが受け付けた受取・返金を写す。精算時の予想現金の計算に使う)
[Name("OrderDeposits")]
public sealed class LocalOrderDepositEntity
{
    [Key]
    public Guid Id { get; set; }

    public Guid OrderId { get; set; }

    public Guid ShiftId { get; set; }

    public OrderDepositType Type { get; set; }

    public Guid PaymentMethodId { get; set; }

    public PaymentKind Kind { get; set; }

    public decimal Amount { get; set; }

    public DateTime OccurredAt { get; set; }
}
