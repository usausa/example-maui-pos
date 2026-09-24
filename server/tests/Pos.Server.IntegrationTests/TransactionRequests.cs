namespace Pos.Server;

using Pos.Contract.Transactions;

// 取引登録の要求を端末と同じ手順で組み立てる (入力項目 → 計算 API → 計算項目を写す)
internal static class TransactionRequests
{
    public static TransactionCalculateRequest ToCalculateRequest(TransactionCreateRequest request) => new()
    {
        Type = request.Type,
        OriginalTransactionId = request.OriginalTransactionId,
        Lines = request.Lines,
        Discounts = request.Discounts,
        Payments = request.Payments
    };

    // 端末が計算した体で、計算項目を応答から写す (明細・値引は並び順で対応)
    public static void Apply(TransactionCreateRequest request, TransactionCalculateResponse calculation)
    {
        for (var i = 0; i < request.Lines.Count; i++)
        {
            var line = request.Lines[i];
            var calculated = calculation.Lines[i];
            line.Amount = calculated.Amount;
            line.DiscountAmount = calculated.DiscountAmount;
            line.AllocatedDiscountAmount = calculated.AllocatedDiscountAmount;
            line.NetAmount = calculated.NetAmount;
            line.PointsRedeemed = calculated.PointsRedeemed;
            line.PointsEarned = calculated.PointsEarned;
        }

        for (var i = 0; i < request.Discounts.Count; i++)
        {
            request.Discounts[i].Amount = calculation.Discounts[i].Amount;
        }

        request.TaxSummaries = calculation.TaxSummaries.Select(static x => new TransactionCreateRequestTaxSummary { TaxRateId = x.TaxRateId, Rate = x.Rate, TaxIncluded = x.TaxIncluded, TaxableAmount = x.TaxableAmount, TaxAmount = x.TaxAmount }).ToList();
        request.Subtotal = calculation.Subtotal;
        request.DiscountTotal = calculation.DiscountTotal;
        request.NetSubtotal = calculation.NetSubtotal;
        request.TaxTotal = calculation.TaxTotal;
        request.Total = calculation.Total;
        request.TenderedTotal = calculation.TenderedTotal;
        request.ChangeAmount = calculation.ChangeAmount;
        request.PointsEarned = calculation.PointsEarned;
        request.PointsRedeemed = calculation.PointsRedeemed;
    }
}
