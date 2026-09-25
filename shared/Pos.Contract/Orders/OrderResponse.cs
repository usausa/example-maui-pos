namespace Pos.Contract.Orders;

using Pos.Contract;

// 受注 (取り寄せ・取り置き)。受注番号はサーバが {店舗コード}-O-{連番} で採番する
public sealed class OrderResponseItem
{
    public Guid Id { get; set; }

    public string OrderNo { get; set; } = default!;

    public Guid StoreId { get; set; }

    public Guid? TerminalId { get; set; }

    public Guid StaffId { get; set; }

    public Guid? CustomerId { get; set; }

    public string CustomerName { get; set; } = default!;

    public string? Phone { get; set; }

    public OrderType Type { get; set; }

    public OrderStatus Status { get; set; }

    public DateOnly? RequestedDate { get; set; }

    public string? Note { get; set; }

    // 明細の金額の合計 (税・値引は会計で決まる)
    public decimal Total { get; set; }

    // 会計した取引 (完了のときだけ)
    public Guid? TransactionId { get; set; }

    public DateTime OrderedAt { get; set; }

    public DateTime? ArrivedAt { get; set; }

    public DateTime? CompletedAt { get; set; }

    public DateTime? CancelledAt { get; set; }

    public string? CancelReason { get; set; }

    // 会計で充てる前受金 (受け取った額 − 返した額。完了した受注は 0)
    public decimal DepositAmount { get; set; }

    public IReadOnlyList<OrderResponseLine> Lines { get; set; } = default!;

    // 前受金の受取と返金の記録
    public IReadOnlyList<OrderResponseDeposit> Deposits { get; set; } = default!;

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public int Version { get; set; }
}

public sealed class OrderResponseLine
{
    public Guid Id { get; set; }

    public int LineNo { get; set; }

    public Guid ProductId { get; set; }

    public string ProductCode { get; set; } = default!;

    public string ProductName { get; set; } = default!;

    public decimal Quantity { get; set; }

    public decimal UnitPrice { get; set; }

    // 単価 × 数量 (切り捨て)
    public decimal Amount { get; set; }

    public string? Note { get; set; }
}

public sealed class OrderResponseDeposit
{
    public Guid Id { get; set; }

    public OrderDepositType Type { get; set; }

    public Guid PaymentMethodId { get; set; }

    public PaymentKind Kind { get; set; }

    public decimal Amount { get; set; }

    // カードの伝票番号など
    public string? Reference { get; set; }

    public Guid TerminalId { get; set; }

    public Guid ShiftId { get; set; }

    public Guid StaffId { get; set; }

    public DateTime OccurredAt { get; set; }
}

public sealed class OrderResponse : ListResponse<OrderResponseItem>;
