namespace Pos.Contract.Transactions;

// 計算項目。POST /transactions/calculate の応答と、CALCULATION_MISMATCH の expected
public sealed class TransactionCalculateResponse
{
    public IReadOnlyList<TransactionCalculateResponseLine> Lines { get; set; } = default!;

    public IReadOnlyList<TransactionCalculateResponseDiscount> Discounts { get; set; } = default!;

    public IReadOnlyList<TransactionCalculateResponseTaxSummary> TaxSummaries { get; set; } = default!;

    public decimal Subtotal { get; set; }

    public decimal DiscountTotal { get; set; }

    public decimal NetSubtotal { get; set; }

    public decimal TaxTotal { get; set; }

    public decimal Total { get; set; }

    public decimal TenderedTotal { get; set; }

    public decimal ChangeAmount { get; set; }

    public int PointsEarned { get; set; }

    public int PointsRedeemed { get; set; }
}

public sealed class TransactionCalculateResponseLine
{
    public Guid Id { get; set; }

    public decimal Amount { get; set; }

    public decimal DiscountAmount { get; set; }

    public decimal AllocatedDiscountAmount { get; set; }

    public decimal NetAmount { get; set; }

    public int PointsRedeemed { get; set; }

    public int PointsEarned { get; set; }
}

public sealed class TransactionCalculateResponseDiscount
{
    public Guid Id { get; set; }

    public decimal Amount { get; set; }
}

public sealed class TransactionCalculateResponseTaxSummary
{
    public Guid TaxRateId { get; set; }

    public decimal Rate { get; set; }

    public bool TaxIncluded { get; set; }

    public decimal TaxableAmount { get; set; }

    public decimal TaxAmount { get; set; }
}
