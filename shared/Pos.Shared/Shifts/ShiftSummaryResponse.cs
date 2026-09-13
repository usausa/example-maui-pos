namespace Pos.Shared.Shifts;

// 精算レポート (GET /shifts/{id}/summary)
public sealed class ShiftSummaryResponse
{
    public ShiftResponse Shift { get; set; } = default!;

    public IReadOnlyList<ShiftSummaryResponsePaymentMethod> ByPaymentMethod { get; set; } = default!;

    public IReadOnlyList<ShiftSummaryResponseTaxRate> ByTaxRate { get; set; } = default!;

    public IReadOnlyList<ShiftSummaryResponseCategory> ByCategory { get; set; } = default!;

    public ShiftSummaryResponsePoints Points { get; set; } = default!;

    public ShiftSummaryResponseCash Cash { get; set; } = default!;
}

public sealed class ShiftSummaryResponsePaymentMethod
{
    public Guid PaymentMethodId { get; set; }

    public string Name { get; set; } = default!;

    public PaymentKind Kind { get; set; }

    public decimal SalesAmount { get; set; }

    public int SalesCount { get; set; }

    public decimal ReturnAmount { get; set; }

    public int ReturnCount { get; set; }
}

public sealed class ShiftSummaryResponseTaxRate
{
    public Guid TaxRateId { get; set; }

    public decimal Rate { get; set; }

    public bool TaxIncluded { get; set; }

    public decimal TaxableAmount { get; set; }

    public decimal TaxAmount { get; set; }
}

public sealed class ShiftSummaryResponseCategory
{
    public Guid CategoryId { get; set; }

    public string Name { get; set; } = default!;

    public decimal Quantity { get; set; }

    public decimal NetAmount { get; set; }
}

public sealed class ShiftSummaryResponsePoints
{
    public int Earned { get; set; }

    public int Redeemed { get; set; }
}

public sealed class ShiftSummaryResponseCash
{
    public decimal OpeningCash { get; set; }

    public decimal CashSales { get; set; }

    public decimal CashReturns { get; set; }

    public decimal PaidIn { get; set; }

    public decimal PaidOut { get; set; }

    public decimal? ExpectedCash { get; set; }

    public decimal? ActualCash { get; set; }

    public decimal? Difference { get; set; }
}
