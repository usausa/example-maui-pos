namespace Pos.Terminal.Services;

using Pos.Shared.Shifts;
using Pos.Shared.Transactions;
using Pos.Terminal.Models.Entity;

// ローカルの取引・入出金からシフト集計を作る (精算画面の予想現金、オフライン時の精算レポート)。サーバの ShiftSummary と同じ形にする
public static class ShiftSummaryBuilder
{
    public static ShiftSummaryResponse Build(
        LocalShiftEntity shift,
        IReadOnlyList<TransactionResponse> transactions,
        IReadOnlyList<LocalCashEventEntity> cashEvents,
        IReadOnlyList<PaymentMethodResponse> paymentMethods,
        IReadOnlyList<CategoryResponse> categories)
    {
        ArgumentNullException.ThrowIfNull(shift);
        ArgumentNullException.ThrowIfNull(transactions);
        ArgumentNullException.ThrowIfNull(cashEvents);
        ArgumentNullException.ThrowIfNull(paymentMethods);
        ArgumentNullException.ThrowIfNull(categories);

        var methodNames = paymentMethods.ToDictionary(static x => x.Id, static x => x);
        var categoryNames = categories.ToDictionary(static x => x.Id, static x => x.Name);

        var byMethod = new Dictionary<Guid, ShiftSummaryResponsePaymentMethod>();
        var byTax = new Dictionary<Guid, ShiftSummaryResponseTaxRate>();
        var byCategory = new Dictionary<Guid, ShiftSummaryResponseCategory>();
        var totals = new ShiftResponseTotals();
        var pointsEarned = 0;
        var pointsRedeemed = 0;

        foreach (var tx in transactions)
        {
            if (tx.Status == TransactionStatus.Voided)
            {
                totals.VoidCount++;
                continue;
            }

            var isReturn = tx.Type == TransactionType.Return;
            if (isReturn)
            {
                totals.ReturnCount++;
                totals.ReturnsTotal += tx.Total;
            }
            else
            {
                totals.SalesCount++;
                totals.SalesTotal += tx.Total;
            }

            pointsEarned += tx.PointsEarned;
            pointsRedeemed += tx.PointsRedeemed;

            foreach (var payment in tx.Payments)
            {
                if (!byMethod.TryGetValue(payment.PaymentMethodId, out var method))
                {
                    var master = methodNames.GetValueOrDefault(payment.PaymentMethodId);
                    method = new ShiftSummaryResponsePaymentMethod { PaymentMethodId = payment.PaymentMethodId, Name = master?.Name ?? DisplayText.Name(payment.Kind), Kind = payment.Kind };
                    byMethod[payment.PaymentMethodId] = method;
                }

                if (isReturn)
                {
                    method.ReturnAmount += payment.Amount;
                    method.ReturnCount++;
                }
                else
                {
                    method.SalesAmount += payment.Amount;
                    method.SalesCount++;
                }

                if (payment.Kind == PaymentKind.Cash)
                {
                    if (isReturn)
                    {
                        totals.CashReturns += payment.Amount;
                    }
                    else
                    {
                        totals.CashSales += payment.Amount;
                    }
                }
            }

            var sign = isReturn ? -1 : 1;
            foreach (var tax in tx.TaxSummaries)
            {
                if (!byTax.TryGetValue(tax.TaxRateId, out var rate))
                {
                    rate = new ShiftSummaryResponseTaxRate { TaxRateId = tax.TaxRateId, Rate = tax.Rate, TaxIncluded = tax.TaxIncluded };
                    byTax[tax.TaxRateId] = rate;
                }

                rate.TaxableAmount += tax.TaxableAmount * sign;
                rate.TaxAmount += tax.TaxAmount * sign;
            }

            foreach (var line in tx.Lines)
            {
                if (!byCategory.TryGetValue(line.CategoryId, out var category))
                {
                    category = new ShiftSummaryResponseCategory { CategoryId = line.CategoryId, Name = categoryNames.GetValueOrDefault(line.CategoryId, "-") };
                    byCategory[line.CategoryId] = category;
                }

                category.Quantity += line.Quantity * sign;
                category.NetAmount += line.NetAmount * sign;
            }
        }

        foreach (var cashEvent in cashEvents)
        {
            switch (cashEvent.Type)
            {
                case CashEventType.PaidIn:
                    totals.PaidIn += cashEvent.Amount;
                    break;
                case CashEventType.PaidOut:
                    totals.PaidOut += cashEvent.Amount;
                    break;
            }
        }

        var expectedCash = shift.OpeningCash + totals.CashSales - totals.CashReturns + totals.PaidIn - totals.PaidOut;

        return new ShiftSummaryResponse
        {
            Shift = new ShiftResponse
            {
                Id = shift.Id,
                StoreId = shift.StoreId,
                TerminalId = shift.TerminalId,
                Status = shift.Status,
                BusinessDate = shift.BusinessDate,
                OpenedAt = shift.OpenedAt,
                OpenedByStaffId = shift.OpenedByStaffId,
                OpeningCash = shift.OpeningCash,
                ClosedAt = shift.ClosedAt,
                ClosedByStaffId = shift.ClosedByStaffId,
                ActualCash = shift.ActualCash,
                ExpectedCash = shift.ExpectedCash ?? expectedCash,
                Difference = shift.Difference,
                Totals = totals,
                Note = shift.Note
            },
            ByPaymentMethod = byMethod.Values.OrderBy(x => methodNames.GetValueOrDefault(x.PaymentMethodId)?.SortOrder ?? Int32.MaxValue).ToList(),
            ByTaxRate = byTax.Values.OrderBy(static x => x.Rate).ToList(),
            ByCategory = byCategory.Values.OrderBy(static x => x.Name, StringComparer.Ordinal).ToList(),
            Points = new ShiftSummaryResponsePoints { Earned = pointsEarned, Redeemed = pointsRedeemed },
            Cash = new ShiftSummaryResponseCash
            {
                OpeningCash = shift.OpeningCash,
                CashSales = totals.CashSales,
                CashReturns = totals.CashReturns,
                PaidIn = totals.PaidIn,
                PaidOut = totals.PaidOut,
                ExpectedCash = expectedCash,
                ActualCash = shift.ActualCash,
                Difference = shift.ActualCash - expectedCash
            }
        };
    }
}
