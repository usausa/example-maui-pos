namespace Pos.Domain.Logic;

// 端末が送った計算項目とサーバの再計算結果の一致判定
public static partial class SalesLogic
{
    public static bool Matches(SalesResult expected, SalesResult actual)
    {
        if ((expected.Subtotal != actual.Subtotal) ||
            (expected.DiscountTotal != actual.DiscountTotal) ||
            (expected.NetSubtotal != actual.NetSubtotal) ||
            (expected.TaxTotal != actual.TaxTotal) ||
            (expected.Total != actual.Total) ||
            (expected.TenderedTotal != actual.TenderedTotal) ||
            (expected.ChangeAmount != actual.ChangeAmount) ||
            (expected.PointsEarned != actual.PointsEarned) ||
            (expected.PointsRedeemed != actual.PointsRedeemed))
        {
            return false;
        }

        if ((expected.Lines.Count != actual.Lines.Count) ||
            (expected.Discounts.Count != actual.Discounts.Count) ||
            (expected.TaxSummaries.Count != actual.TaxSummaries.Count))
        {
            return false;
        }

        foreach (var line in expected.Lines)
        {
            var other = actual.Lines.FirstOrDefault(x => x.Id == line.Id);
            if ((other is null) ||
                (other.Amount != line.Amount) ||
                (other.DiscountAmount != line.DiscountAmount) ||
                (other.AllocatedDiscountAmount != line.AllocatedDiscountAmount) ||
                (other.NetAmount != line.NetAmount) ||
                (other.PointsRedeemed != line.PointsRedeemed) ||
                (other.PointsEarned != line.PointsEarned))
            {
                return false;
            }
        }

        foreach (var discount in expected.Discounts)
        {
            var other = actual.Discounts.FirstOrDefault(x => x.Id == discount.Id);
            if ((other is null) || (other.Amount != discount.Amount))
            {
                return false;
            }
        }

        foreach (var summary in expected.TaxSummaries)
        {
            var other = actual.TaxSummaries.FirstOrDefault(x => (x.TaxRateId == summary.TaxRateId) && (x.TaxIncluded == summary.TaxIncluded));
            if ((other is null) ||
                (other.Rate != summary.Rate) ||
                (other.TaxableAmount != summary.TaxableAmount) ||
                (other.TaxAmount != summary.TaxAmount))
            {
                return false;
            }
        }

        return true;
    }
}
