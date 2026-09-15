namespace Pos.Domain.Logic;

// 販売計算。入力は変更しない
public static partial class SalesLogic
{
    public static SalesResult Calculate(SalesInput input)
    {
        var lines = input.Lines;
        var count = lines.Count;

        var lineIndexes = new Dictionary<Guid, int>(count);
        for (var i = 0; i < count; i++)
        {
            lineIndexes[lines[i].Id] = i;
        }

        // 明細
        var amounts = new decimal[count];
        for (var i = 0; i < count; i++)
        {
            amounts[i] = Math.Floor(lines[i].UnitPrice * lines[i].Quantity);
        }

        var lineDiscountAmounts = new decimal[count];
        var discountAmounts = new decimal[input.Discounts.Count];
        for (var d = 0; d < input.Discounts.Count; d++)
        {
            var discount = input.Discounts[d];
            if (discount.LineId is null)
            {
                continue;
            }

            if (!lineIndexes.TryGetValue(discount.LineId.Value, out var index))
            {
                throw new ArgumentException($"Discount line not found. lineId=[{discount.LineId}]", nameof(input));
            }

            var amount = discount.Type == DiscountType.Amount
                ? discount.Value
                : Math.Floor(amounts[index] * discount.Value);
            discountAmounts[d] = amount;
            lineDiscountAmounts[index] += amount;
        }

        // 取引値引の按分
        var bases = new decimal[count];
        var baseTotal = 0m;
        for (var i = 0; i < count; i++)
        {
            bases[i] = amounts[i] - lineDiscountAmounts[i];
            baseTotal += bases[i];
        }

        var transactionDiscount = 0m;
        for (var d = 0; d < input.Discounts.Count; d++)
        {
            var discount = input.Discounts[d];
            if (discount.LineId is not null)
            {
                continue;
            }

            var amount = discount.Type == DiscountType.Amount
                ? discount.Value
                : Math.Floor(baseTotal * discount.Value);
            discountAmounts[d] = amount;
            transactionDiscount += amount;
        }

        var allocatedDiscounts = AllocationLogic.Allocate(transactionDiscount, bases);

        var netAmounts = new decimal[count];
        for (var i = 0; i < count; i++)
        {
            netAmounts[i] = bases[i] - allocatedDiscounts[i];
        }

        // 税 (税率 × 内税/外税 のグループごと)
        var taxLines = new TaxLine[count];
        for (var i = 0; i < count; i++)
        {
            taxLines[i] = new TaxLine(lines[i].TaxRateId, lines[i].TaxRate, lines[i].TaxIncluded, netAmounts[i]);
        }

        var tax = TaxLogic.Calculate(taxLines, input.TaxRounding);
        var taxSummaries = tax.Summaries;
        var allocatedTaxes = tax.AllocatedTaxes;

        // ポイント
        var pointsRedeemedTotal = 0m;
        foreach (var payment in input.Payments)
        {
            if (payment.Kind == PaymentKind.Points)
            {
                pointsRedeemedTotal += payment.Amount;
            }
        }

        var pointsRedeemed = AllocationLogic.Allocate(pointsRedeemedTotal, netAmounts);
        var pointsEarned = new int[count];
        var pointsEarnedTotal = 0;
        for (var i = 0; i < count; i++)
        {
            var line = lines[i];
            var pointBase = input.PointBasis == PointBasis.TaxIncluded
                ? (line.TaxIncluded ? netAmounts[i] : netAmounts[i] + allocatedTaxes[i])
                : (line.TaxIncluded ? netAmounts[i] - allocatedTaxes[i] : netAmounts[i]);
            var earned = (int)Math.Floor((pointBase - pointsRedeemed[i]) * line.PointRate);
            pointsEarned[i] = Math.Max(0, earned);
            pointsEarnedTotal += pointsEarned[i];
        }

        // 合計・預り・釣銭
        var subtotal = 0m;
        var lineDiscountTotal = 0m;
        var netSubtotal = 0m;
        for (var i = 0; i < count; i++)
        {
            subtotal += amounts[i];
            lineDiscountTotal += lineDiscountAmounts[i];
            netSubtotal += netAmounts[i];
        }

        var taxTotal = 0m;
        var excludedTaxTotal = 0m;
        foreach (var summary in taxSummaries)
        {
            taxTotal += summary.TaxAmount;
            if (!summary.TaxIncluded)
            {
                excludedTaxTotal += summary.TaxAmount;
            }
        }

        var total = netSubtotal + excludedTaxTotal;

        var tenderedTotal = 0m;
        foreach (var payment in input.Payments)
        {
            tenderedTotal += payment.TenderedAmount;
        }

        var resultLines = new SalesResultLine[count];
        for (var i = 0; i < count; i++)
        {
            resultLines[i] = new SalesResultLine
            {
                Id = lines[i].Id,
                LineNo = lines[i].LineNo,
                Amount = amounts[i],
                DiscountAmount = lineDiscountAmounts[i],
                AllocatedDiscountAmount = allocatedDiscounts[i],
                NetAmount = netAmounts[i],
                PointsRedeemed = (int)pointsRedeemed[i],
                PointsEarned = pointsEarned[i]
            };
        }

        var resultDiscounts = new SalesResultDiscount[input.Discounts.Count];
        for (var d = 0; d < input.Discounts.Count; d++)
        {
            resultDiscounts[d] = new SalesResultDiscount
            {
                Id = input.Discounts[d].Id,
                Amount = discountAmounts[d]
            };
        }

        return new SalesResult
        {
            Lines = resultLines,
            Discounts = resultDiscounts,
            TaxSummaries = taxSummaries,
            Subtotal = subtotal,
            DiscountTotal = lineDiscountTotal + transactionDiscount,
            NetSubtotal = netSubtotal,
            TaxTotal = taxTotal,
            Total = total,
            TenderedTotal = tenderedTotal,
            ChangeAmount = tenderedTotal - total,
            PointsEarned = pointsEarnedTotal,
            PointsRedeemed = (int)pointsRedeemedTotal
        };
    }
}
