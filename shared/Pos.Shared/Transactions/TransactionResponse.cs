namespace Pos.Shared.Transactions;

using Pos.Shared.Common;

// 取引 (api-design §3.12)。TransactionRequest と同じ形にサーバ付与項目が付く
public sealed class TransactionResponse
{
    public Guid Id { get; set; }

    public TransactionType Type { get; set; }

    public TransactionStatus Status { get; set; }

    public Guid StoreId { get; set; }

    public Guid TerminalId { get; set; }

    public Guid StaffId { get; set; }

    public Guid ShiftId { get; set; }

    public Guid? CustomerId { get; set; }

    public string ReceiptNo { get; set; } = default!;

    public DateOnly BusinessDate { get; set; }

    public DateTime TransactedAt { get; set; }

    public Guid? OriginalTransactionId { get; set; }

    public IReadOnlyList<TransactionResponseLine> Lines { get; set; } = default!;

    public IReadOnlyList<TransactionResponseDiscount> Discounts { get; set; } = default!;

    public IReadOnlyList<TransactionResponseTaxSummary> TaxSummaries { get; set; } = default!;

    public decimal Subtotal { get; set; }

    public decimal DiscountTotal { get; set; }

    public decimal NetSubtotal { get; set; }

    public decimal TaxTotal { get; set; }

    public decimal Total { get; set; }

    public IReadOnlyList<TransactionResponsePayment> Payments { get; set; } = default!;

    public decimal TenderedTotal { get; set; }

    public decimal ChangeAmount { get; set; }

    public int PointsEarned { get; set; }

    public int PointsRedeemed { get; set; }

    // 処理後残高 (サーバ)
    public int? PointsBalanceAfter { get; set; }

    public TransactionResponseDelivery? Delivery { get; set; }

    public string? Note { get; set; }

    public TransactionResponseVoid? Void { get; set; }

    // 受理したが確認が必要な事項 (api-design §5 の警告コード)
    public IReadOnlyList<TransactionResponseWarning> Warnings { get; set; } = [];

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }
}

public sealed class TransactionResponseLine
{
    public Guid Id { get; set; }

    public int LineNo { get; set; }

    public Guid ProductId { get; set; }

    public string ProductCode { get; set; } = default!;

    public string ProductName { get; set; } = default!;

    public Guid CategoryId { get; set; }

    public ProductKind Kind { get; set; }

    public decimal ListPrice { get; set; }

    public decimal UnitPrice { get; set; }

    public decimal Quantity { get; set; }

    public Guid TaxRateId { get; set; }

    public decimal TaxRate { get; set; }

    public bool TaxIncluded { get; set; }

    public decimal PointRate { get; set; }

    public decimal Amount { get; set; }

    public decimal DiscountAmount { get; set; }

    public decimal AllocatedDiscountAmount { get; set; }

    public decimal NetAmount { get; set; }

    public int PointsRedeemed { get; set; }

    public int PointsEarned { get; set; }

    public IReadOnlyList<string> SerialNumbers { get; set; } = [];

    public Guid? OriginalLineId { get; set; }

    // 返品済み数量 (元取引側で更新される)
    public decimal ReturnedQuantity { get; set; }

    public string? Note { get; set; }
}

public sealed class TransactionResponseDiscount
{
    public Guid Id { get; set; }

    public Guid? LineId { get; set; }

    public Guid? DiscountId { get; set; }

    public string Name { get; set; } = default!;

    public DiscountType Type { get; set; }

    public decimal Value { get; set; }

    public decimal Amount { get; set; }

    public string? Reason { get; set; }

    public Guid? ApprovedByStaffId { get; set; }
}

public sealed class TransactionResponseTaxSummary
{
    public Guid TaxRateId { get; set; }

    public decimal Rate { get; set; }

    public bool TaxIncluded { get; set; }

    public decimal TaxableAmount { get; set; }

    public decimal TaxAmount { get; set; }
}

public sealed class TransactionResponsePayment
{
    public Guid Id { get; set; }

    public int SeqNo { get; set; }

    public Guid PaymentMethodId { get; set; }

    public PaymentKind Kind { get; set; }

    public decimal Amount { get; set; }

    public decimal TenderedAmount { get; set; }

    public string? Reference { get; set; }

    public string? Note { get; set; }
}

public sealed class TransactionResponseDelivery
{
    public string RecipientName { get; set; } = default!;

    public string? Phone { get; set; }

    public string? PostalCode { get; set; }

    public string Address { get; set; } = default!;

    public DateOnly? RequestedDate { get; set; }

    public string? TimeSlot { get; set; }

    public string? Note { get; set; }
}

public sealed class TransactionResponseVoid
{
    public DateTime VoidedAt { get; set; }

    public Guid VoidedByStaffId { get; set; }

    public string Reason { get; set; } = default!;
}

public sealed class TransactionResponseWarning
{
    // WarningCode.ToCode() (UPPER_SNAKE_CASE)
    public string Code { get; set; } = default!;

    public string Message { get; set; } = default!;

    public Guid? LineId { get; set; }
}

public sealed class TransactionListResponse : ListResponse<TransactionResponse>
{
}
