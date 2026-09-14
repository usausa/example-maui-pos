namespace Pos.Server.Models.Views;

// シフトの集計 (取消済みを除く)。Open 中は取引から都度集計し、精算時に Shifts へ確定する
public sealed record ShiftTotals(
    decimal CashSales,
    decimal CashReturns,
    decimal PaidIn,
    decimal PaidOut,
    int SalesCount,
    int ReturnCount,
    int VoidCount,
    decimal SalesTotal,
    decimal ReturnsTotal)
{
    public static ShiftTotals Empty { get; } = new(0, 0, 0, 0, 0, 0, 0, 0, 0);
}
