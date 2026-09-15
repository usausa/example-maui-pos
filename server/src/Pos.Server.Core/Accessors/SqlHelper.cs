namespace Pos.Server.Accessors;

using Pos.Server.Models.Enums;

// 2-way SQL の /*# */ から呼ぶ SQL 断片。生 SQL へ渡す値は閉じた集合 (列挙型) から作る
public static class SqlHelper
{
    // 売上集計の GROUP BY 式 (時間帯・支払方法・税率・部門は専用クエリ)
    public static string GroupKey(SalesSummaryGroupBy groupBy) =>
        groupBy switch
        {
            SalesSummaryGroupBy.Store => "t.StoreId",
            SalesSummaryGroupBy.Terminal => "t.TerminalId",
            SalesSummaryGroupBy.Staff => "t.StaffId",
            _ => "t.BusinessDate"
        };

    public static string GroupLabel(SalesSummaryGroupBy groupBy) =>
        groupBy switch
        {
            SalesSummaryGroupBy.Store => "COALESCE(st.Name, t.StoreId)",
            SalesSummaryGroupBy.Terminal => "COALESCE(tm.Name, t.TerminalId)",
            SalesSummaryGroupBy.Staff => "COALESCE(s.Name, t.StaffId)",
            _ => "t.BusinessDate"
        };

    // 商品別売上の並び順の列
    public static string ProductSalesColumn(ProductSalesSort sort) =>
        sort == ProductSalesSort.Quantity ? "NetQuantity" : "NetSales";
}
