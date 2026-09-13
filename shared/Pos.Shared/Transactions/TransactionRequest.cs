namespace Pos.Shared.Transactions;

// 取引登録 (api-design §3.12)。「計算」区分の項目は端末が計算し、サーバが再計算して検証する
public sealed class TransactionRequest
{
    // 端末採番
    public Guid Id { get; set; }

    public TransactionType Type { get; set; }

    // オフライン中に取消した取引は Voided + Void 付きで送れる
    public TransactionStatus Status { get; set; }

    public Guid StoreId { get; set; }

    public Guid TerminalId { get; set; }

    public Guid StaffId { get; set; }

    public Guid ShiftId { get; set; }

    // ポイント付与・利用時は必須
    public Guid? CustomerId { get; set; }

    // {店舗コード}-{端末番号:00}-{連番:000000}
    [Required]
    [MaxLength(20)]
    public string ReceiptNo { get; set; } = default!;

    public DateOnly BusinessDate { get; set; }

    public DateTime TransactedAt { get; set; }

    // Return のとき必須
    public Guid? OriginalTransactionId { get; set; }

    [Required]
    public IReadOnlyList<TransactionRequestLine> Lines { get; set; } = default!;

    public IReadOnlyList<TransactionRequestDiscount> Discounts { get; set; } = [];

    public IReadOnlyList<TransactionRequestTaxSummary> TaxSummaries { get; set; } = [];

    public decimal Subtotal { get; set; }

    public decimal DiscountTotal { get; set; }

    public decimal NetSubtotal { get; set; }

    public decimal TaxTotal { get; set; }

    public decimal Total { get; set; }

    // Return では返金方法
    [Required]
    public IReadOnlyList<TransactionRequestPayment> Payments { get; set; } = default!;

    public decimal TenderedTotal { get; set; }

    public decimal ChangeAmount { get; set; }

    // Return では取消分を負で持つ
    public int PointsEarned { get; set; }

    // Return では返還分を負で持つ
    public int PointsRedeemed { get; set; }

    public TransactionRequestDelivery? Delivery { get; set; }

    [MaxLength(500)]
    public string? Note { get; set; }

    public TransactionRequestVoid? Void { get; set; }
}

public sealed class TransactionRequestLine
{
    public Guid Id { get; set; }

    // 1 始まり
    public int LineNo { get; set; }

    public Guid ProductId { get; set; }

    // 以下は販売時点のスナップショット
    [Required]
    [MaxLength(20)]
    public string ProductCode { get; set; } = default!;

    [Required]
    [MaxLength(100)]
    public string ProductName { get; set; } = default!;

    public Guid CategoryId { get; set; }

    public ProductKind Kind { get; set; }

    // 販売時点のマスタ価格
    public decimal ListPrice { get; set; }

    // 適用単価。allowsPriceOverride の商品以外は ListPrice と一致すること
    public decimal UnitPrice { get; set; }

    public decimal Quantity { get; set; }

    public Guid TaxRateId { get; set; }

    public decimal TaxRate { get; set; }

    public bool TaxIncluded { get; set; }

    public decimal PointRate { get; set; }

    // 計算
    public decimal Amount { get; set; }

    public decimal DiscountAmount { get; set; }

    public decimal AllocatedDiscountAmount { get; set; }

    public decimal NetAmount { get; set; }

    public int PointsRedeemed { get; set; }

    public int PointsEarned { get; set; }

    public IReadOnlyList<string> SerialNumbers { get; set; } = [];

    // Return のとき必須。元取引の明細
    public Guid? OriginalLineId { get; set; }

    [MaxLength(200)]
    public string? Note { get; set; }
}

public sealed class TransactionRequestDiscount
{
    public Guid Id { get; set; }

    // 対象明細。null = 取引値引
    public Guid? LineId { get; set; }

    // 定義済み値引。null = 任意値引
    public Guid? DiscountId { get; set; }

    [Required]
    [MaxLength(50)]
    public string Name { get; set; } = default!;

    public DiscountType Type { get; set; }

    public decimal Value { get; set; }

    // 計算: 値引額
    public decimal Amount { get; set; }

    [MaxLength(200)]
    public string? Reason { get; set; }

    public Guid? ApprovedByStaffId { get; set; }
}

public sealed class TransactionRequestTaxSummary
{
    public Guid TaxRateId { get; set; }

    public decimal Rate { get; set; }

    public bool TaxIncluded { get; set; }

    public decimal TaxableAmount { get; set; }

    public decimal TaxAmount { get; set; }
}

public sealed class TransactionRequestPayment
{
    public Guid Id { get; set; }

    public int SeqNo { get; set; }

    public Guid PaymentMethodId { get; set; }

    public PaymentKind Kind { get; set; }

    // 充当額。Σ = Total
    public decimal Amount { get; set; }

    // 預り額。allowsChange の方法以外は Amount と同じ
    public decimal TenderedAmount { get; set; }

    // カード伝票番号など
    [MaxLength(50)]
    public string? Reference { get; set; }

    [MaxLength(200)]
    public string? Note { get; set; }
}

public sealed class TransactionRequestDelivery
{
    [Required]
    [MaxLength(100)]
    public string RecipientName { get; set; } = default!;

    [MaxLength(20)]
    public string? Phone { get; set; }

    [MaxLength(10)]
    public string? PostalCode { get; set; }

    [Required]
    [MaxLength(200)]
    public string Address { get; set; } = default!;

    public DateOnly? RequestedDate { get; set; }

    [MaxLength(20)]
    public string? TimeSlot { get; set; }

    [MaxLength(200)]
    public string? Note { get; set; }
}

public sealed class TransactionRequestVoid
{
    public DateTime VoidedAt { get; set; }

    public Guid VoidedByStaffId { get; set; }

    [Required]
    [MaxLength(200)]
    public string Reason { get; set; } = default!;
}
