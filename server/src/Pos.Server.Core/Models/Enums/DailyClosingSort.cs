namespace Pos.Server.Models.Enums;

// 日次締め一覧の並び順 (列挙名 = 列名)。先頭が既定。同じ値の中は店舗コード順
public enum DailyClosingSort
{
    BusinessDate,
    NetSales
}
