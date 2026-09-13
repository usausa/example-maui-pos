namespace Pos.Server.Host.Mappers;

using Pos.Server.Accessors;
using Pos.Server.Models;
using Pos.Server.Models.Entity;
using Pos.Shared.Shifts;

using Smart.Mapper;

public static partial class ShiftMapper
{
    [Mapper]
    private static partial ShiftResponse ToResponseCore(ShiftEntity entity);

    [Mapper]
    public static partial ShiftResponseDenomination ToResponseDenomination(ShiftDenominationEntity entity);

    [Mapper]
    public static partial ShiftResponseTotals ToResponseTotals(ShiftTotals totals);

    [Mapper]
    public static partial CashEventResponse ToCashEventResponse(CashEventEntity entity);

    [Mapper]
    public static partial CashEventEntity ToCashEventEntity(CashEventRequest request);

    [Mapper]
    public static partial ShiftEntity ToEntity(ShiftOpenRequest request);

    [Mapper]
    public static partial ShiftSummaryResponsePaymentMethod ToSummaryPaymentMethod(PaymentMethodTotal total);

    [Mapper]
    public static partial ShiftSummaryResponseTaxRate ToSummaryTaxRate(TaxRateTotal total);

    [Mapper]
    public static partial ShiftSummaryResponseCategory ToSummaryCategory(CategoryTotal total);

    // Open 中は取引から都度集計し、Closed は確定値 (Shifts の列) を使う
    public static async ValueTask<ShiftResponse> ToResponseAsync(ShiftAccessor accessor, ShiftEntity entity, CancellationToken cancellationToken)
    {
        var totals = await ResolveTotalsAsync(accessor, entity, cancellationToken);
        var denominations = await accessor.QueryDenominationsAsync(entity.Id, cancellationToken);

        var response = ToResponseCore(entity);
        response.Totals = ToResponseTotals(totals);
        response.Denominations = denominations.Select(ToResponseDenomination).ToList();
        if (entity.Status == ShiftStatus.Open)
        {
            response.ExpectedCash = ExpectedCash(entity.OpeningCash, totals);
        }

        return response;
    }

    public static async ValueTask<ShiftSummaryResponse> ToSummaryResponseAsync(ShiftAccessor accessor, ShiftEntity entity, CancellationToken cancellationToken)
    {
        var shift = await ToResponseAsync(accessor, entity, cancellationToken);
        var byPaymentMethod = await accessor.QueryPaymentMethodTotalsAsync(entity.Id, cancellationToken);
        var byTaxRate = await accessor.QueryTaxRateTotalsAsync(entity.Id, cancellationToken);
        var byCategory = await accessor.QueryCategoryTotalsAsync(entity.Id, cancellationToken);
        var points = await accessor.QueryPointTotalsAsync(entity.Id, cancellationToken) ?? new PointTotals(0, 0);

        return new ShiftSummaryResponse
        {
            Shift = shift,
            ByPaymentMethod = byPaymentMethod.Select(ToSummaryPaymentMethod).ToList(),
            ByTaxRate = byTaxRate.Select(ToSummaryTaxRate).ToList(),
            ByCategory = byCategory.Select(ToSummaryCategory).ToList(),
            Points = new ShiftSummaryResponsePoints { Earned = points.Earned, Redeemed = points.Redeemed },
            Cash = new ShiftSummaryResponseCash
            {
                OpeningCash = shift.OpeningCash,
                CashSales = shift.Totals.CashSales,
                CashReturns = shift.Totals.CashReturns,
                PaidIn = shift.Totals.PaidIn,
                PaidOut = shift.Totals.PaidOut,
                ExpectedCash = shift.ExpectedCash,
                ActualCash = shift.ActualCash,
                Difference = shift.Difference
            }
        };
    }

    public static async ValueTask<ShiftTotals> ResolveTotalsAsync(ShiftAccessor accessor, ShiftEntity entity, CancellationToken cancellationToken)
    {
        if (entity.Status == ShiftStatus.Closed)
        {
            return new ShiftTotals(entity.CashSales, entity.CashReturns, entity.PaidIn, entity.PaidOut, entity.SalesCount, entity.ReturnCount, entity.VoidCount, entity.SalesTotal, entity.ReturnsTotal);
        }

        return await accessor.QueryTotalsAsync(entity.Id, cancellationToken) ?? new ShiftTotals(0, 0, 0, 0, 0, 0, 0, 0, 0);
    }

    // openingCash + cashSales − cashReturns + paidIn − paidOut
    public static decimal ExpectedCash(decimal openingCash, ShiftTotals totals) =>
        openingCash + totals.CashSales - totals.CashReturns + totals.PaidIn - totals.PaidOut;
}
