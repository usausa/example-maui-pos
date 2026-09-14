namespace Pos.Server.Models;

// 各項目は Host の Mapper (ソース生成) が読む
// ReSharper disable NotAccessedPositionalProperty.Global
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
    decimal ReturnsTotal);

public sealed record PaymentMethodTotal(
    Guid PaymentMethodId,
    string Name,
    PaymentKind Kind,
    decimal SalesAmount,
    int SalesCount,
    decimal ReturnAmount,
    int ReturnCount);

// 返品は負として合算
public sealed record TaxRateTotal(
    Guid TaxRateId,
    decimal Rate,
    bool TaxIncluded,
    decimal TaxableAmount,
    decimal TaxAmount);

public sealed record CategoryTotal(
    Guid CategoryId,
    string Name,
    decimal Quantity,
    decimal NetAmount);

public sealed record PointTotals(int Earned, int Redeemed);
