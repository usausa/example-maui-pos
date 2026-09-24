namespace Pos.Contract.DailyClosings;

// 日次締めの内容 (POST /daily-closings、GET /daily-closings/{id}、GET /daily-closings/preview)。
// 締め済みは締めた時点の日計と内訳、未締めは取引からの集計。シフトは現在の状態
public sealed class DailyClosingSummaryResponse
{
    public DailyClosingResponseItem DailyClosing { get; set; } = default!;

    public IReadOnlyList<DailyClosingSummaryResponsePaymentMethod> ByPaymentMethod { get; set; } = default!;

    public IReadOnlyList<DailyClosingSummaryResponseTaxRate> ByTaxRate { get; set; } = default!;

    public IReadOnlyList<DailyClosingSummaryResponseShift> Shifts { get; set; } = default!;
}

public sealed class DailyClosingSummaryResponsePaymentMethod
{
    public Guid PaymentMethodId { get; set; }

    public string Name { get; set; } = default!;

    public PaymentKind Kind { get; set; }

    public decimal SalesAmount { get; set; }

    public int SalesCount { get; set; }

    public decimal ReturnAmount { get; set; }

    public int ReturnCount { get; set; }
}

public sealed class DailyClosingSummaryResponseTaxRate
{
    public Guid TaxRateId { get; set; }

    public decimal Rate { get; set; }

    public bool TaxIncluded { get; set; }

    public decimal TaxableAmount { get; set; }

    public decimal TaxAmount { get; set; }
}

public sealed class DailyClosingSummaryResponseShift
{
    public Guid Id { get; set; }

    public Guid TerminalId { get; set; }

    public ShiftStatus Status { get; set; }

    // シフトの営業日 (前日に開設して日をまたいだシフトは締める日と異なる)
    public DateOnly BusinessDate { get; set; }

    public DateTime OpenedAt { get; set; }

    public Guid OpenedByStaffId { get; set; }

    public DateTime? ClosedAt { get; set; }

    public Guid? ClosedByStaffId { get; set; }

    public decimal? ExpectedCash { get; set; }

    public decimal? ActualCash { get; set; }

    public decimal? Difference { get; set; }
}
