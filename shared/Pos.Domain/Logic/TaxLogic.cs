namespace Pos.Domain.Logic;

internal readonly record struct TaxLine(Guid TaxRateId, decimal Rate, bool TaxIncluded, decimal NetAmount);

internal sealed class TaxResult
{
    public required IReadOnlyList<SalesResultTaxSummary> Summaries { get; init; }

    // 明細ごとの按分税額 (ポイントの基準用。保存はしない)
    public required decimal[] AllocatedTaxes { get; init; }
}

// 税計算。税率 × 内税/外税 のグループごとに合計してから税額を計算する
internal static class TaxLogic
{
    public static TaxResult Calculate(IReadOnlyList<TaxLine> lines, TaxRounding rounding)
    {
        var count = lines.Count;
        var groupKeys = new List<(Guid TaxRateId, bool TaxIncluded)>();
        var groupIndexes = new Dictionary<(Guid, bool), int>();
        var groupLines = new List<List<int>>();
        for (var i = 0; i < count; i++)
        {
            var key = (lines[i].TaxRateId, lines[i].TaxIncluded);
            if (!groupIndexes.TryGetValue(key, out var group))
            {
                group = groupKeys.Count;
                groupIndexes[key] = group;
                groupKeys.Add(key);
                groupLines.Add([]);
            }

            groupLines[group].Add(i);
        }

        var summaries = new SalesResultTaxSummary[groupKeys.Count];
        var allocatedTaxes = new decimal[count];
        for (var g = 0; g < groupKeys.Count; g++)
        {
            var members = groupLines[g];
            var rate = lines[members[0]].Rate;
            var taxIncluded = groupKeys[g].TaxIncluded;

            var taxableAmount = 0m;
            var weights = new decimal[members.Count];
            for (var m = 0; m < members.Count; m++)
            {
                weights[m] = lines[members[m]].NetAmount;
                taxableAmount += weights[m];
            }

            var taxAmount = RoundingLogic.Apply(
                taxIncluded ? taxableAmount * rate / (1m + rate) : taxableAmount * rate,
                rounding);

            summaries[g] = new SalesResultTaxSummary
            {
                TaxRateId = groupKeys[g].TaxRateId,
                Rate = rate,
                TaxIncluded = taxIncluded,
                TaxableAmount = taxableAmount,
                TaxAmount = taxAmount
            };

            var allocated = AllocationLogic.Allocate(taxAmount, weights);
            for (var m = 0; m < members.Count; m++)
            {
                allocatedTaxes[members[m]] = allocated[m];
            }
        }

        return new TaxResult
        {
            Summaries = summaries,
            AllocatedTaxes = allocatedTaxes
        };
    }
}
