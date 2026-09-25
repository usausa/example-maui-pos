namespace Pos.Contract.Shifts;

using Pos.Contract;

// シフト
public sealed class ShiftResponseItem
{
    public Guid Id { get; set; }

    public Guid StoreId { get; set; }

    public Guid TerminalId { get; set; }

    public ShiftStatus Status { get; set; }

    public DateOnly BusinessDate { get; set; }

    public DateTime OpenedAt { get; set; }

    public Guid OpenedByStaffId { get; set; }

    // 釣銭準備金
    public decimal OpeningCash { get; set; }

    public DateTime? ClosedAt { get; set; }

    public Guid? ClosedByStaffId { get; set; }

    // 実査金額
    public decimal? ActualCash { get; set; }

    public IReadOnlyList<ShiftResponseDenomination> Denominations { get; set; } = [];

    // openingCash + cashSales − cashReturns + paidIn − paidOut
    public decimal? ExpectedCash { get; set; }

    // actualCash − expectedCash
    public decimal? Difference { get; set; }

    public ShiftResponseTotals Totals { get; set; } = default!;

    public string? Note { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }
}

public sealed class ShiftResponseDenomination
{
    public int Denomination { get; set; }

    public int Count { get; set; }
}

// 取消済みを除く集計
public sealed class ShiftResponseTotals
{
    public decimal CashSales { get; set; }

    public decimal CashReturns { get; set; }

    public decimal PaidIn { get; set; }

    public decimal PaidOut { get; set; }

    // 現金で受け取った前受金と、現金で返した前受金 (予想現金に入る)
    public decimal DepositCashIn { get; set; }

    public decimal DepositCashOut { get; set; }

    public int SalesCount { get; set; }

    public int ReturnCount { get; set; }

    public int VoidCount { get; set; }

    public decimal SalesTotal { get; set; }

    public decimal ReturnsTotal { get; set; }
}

public sealed class ShiftResponse : ListResponse<ShiftResponseItem>;
