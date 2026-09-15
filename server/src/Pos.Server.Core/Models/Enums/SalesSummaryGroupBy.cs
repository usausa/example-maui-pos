namespace Pos.Server.Models.Enums;

// 売上集計のグループ。時間帯・支払方法・税率・部門は専用クエリ、それ以外は取引テーブルの GROUP BY
public enum SalesSummaryGroupBy
{
    Day,
    Store,
    Hour,
    Terminal,
    Staff,
    PaymentMethod,
    TaxRate,
    Category
}
