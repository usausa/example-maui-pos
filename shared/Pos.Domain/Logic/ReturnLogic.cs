namespace Pos.Domain.Logic;

// 返品計算。元明細から返品明細を導出し、税は販売と同じ方法で再計算する
public static class ReturnLogic
{
    public static SalesResult Calculate(ReturnInput input)
    {
        var lines = input.Lines;
        var count = lines.Count;

        var originals = new Dictionary<Guid, ReturnOriginalLine>(input.OriginalLines.Count);
        foreach (var original in input.OriginalLines)
        {
            originals[original.Id] = original;
        }

        var resultLines = new SalesResultLine[count];
        var taxLines = new TaxLine[count];
        var subtotal = 0m;
        var discountTotal = 0m;
        var netSubtotal = 0m;
        var pointsEarnedTotal = 0;
        var pointsRedeemedTotal = 0;
        for (var i = 0; i < count; i++)
        {
            var line = lines[i];
            if (!originals.TryGetValue(line.OriginalLineId, out var original))
            {
                throw new ArgumentException($"Original line not found. originalLineId=[{line.OriginalLineId}]", nameof(input));
            }

            var quantity = line.Quantity;

            var amount = Math.Floor(original.UnitPrice * quantity);
            var discountAmount = Math.Floor(original.DiscountAmount * quantity / original.Quantity);
            var allocatedDiscountAmount = Math.Floor(original.AllocatedDiscountAmount * quantity / original.Quantity);
            var netAmount = amount - discountAmount - allocatedDiscountAmount;
            var pointsEarned = -(int)Math.Floor(original.PointsEarned * quantity / original.Quantity);
            var pointsRedeemed = -(int)Math.Floor(original.PointsRedeemed * quantity / original.Quantity);

            resultLines[i] = new SalesResultLine
            {
                Id = line.Id,
                LineNo = line.LineNo,
                Amount = amount,
                DiscountAmount = discountAmount,
                AllocatedDiscountAmount = allocatedDiscountAmount,
                NetAmount = netAmount,
                PointsRedeemed = pointsRedeemed,
                PointsEarned = pointsEarned
            };
            taxLines[i] = new TaxLine(original.TaxRateId, original.TaxRate, original.TaxIncluded, netAmount);

            subtotal += amount;
            discountTotal += discountAmount + allocatedDiscountAmount;
            netSubtotal += netAmount;
            pointsEarnedTotal += pointsEarned;
            pointsRedeemedTotal += pointsRedeemed;
        }

        var tax = TaxLogic.Calculate(taxLines, input.TaxRounding);

        var taxTotal = 0m;
        var excludedTaxTotal = 0m;
        foreach (var summary in tax.Summaries)
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

        return new SalesResult
        {
            Lines = resultLines,
            Discounts = [],
            TaxSummaries = tax.Summaries,
            Subtotal = subtotal,
            DiscountTotal = discountTotal,
            NetSubtotal = netSubtotal,
            TaxTotal = taxTotal,
            Total = total,
            TenderedTotal = tenderedTotal,
            ChangeAmount = tenderedTotal - total,
            PointsEarned = pointsEarnedTotal,
            PointsRedeemed = pointsRedeemedTotal
        };
    }
}
