namespace Pos.Server.Infrastructure.Data;

using Pos.Server.Models;

// 売上集計の GROUP BY 式。生 SQL (/*# */) へ渡すので閉じた集合から選ぶ
public static class ReportSql
{
    public static string GroupKey(SalesSummaryGroup group) =>
        group switch
        {
            SalesSummaryGroup.Terminal => "t.TerminalId",
            SalesSummaryGroup.Staff => "t.StaffId",
            _ => "t.BusinessDate"
        };

    public static string GroupLabel(SalesSummaryGroup group) =>
        group switch
        {
            SalesSummaryGroup.Terminal => "COALESCE(tm.Name, t.TerminalId)",
            SalesSummaryGroup.Staff => "COALESCE(s.Name, t.StaffId)",
            _ => "t.BusinessDate"
        };
}
