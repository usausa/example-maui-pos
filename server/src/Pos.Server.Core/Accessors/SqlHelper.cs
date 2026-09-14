namespace Pos.Server.Accessors;

using System.Data.Common;

using Pos.Server.Models.Parameters;

// SQL 文字列の組み立てとスキーマ操作の補助。生 SQL 出力 (/*# */) へ渡す値は閉じた集合から作る
public static class SqlHelper
{
    // 差分同期 (updatedSince 指定時) の並び順
    public const string SyncSort = "UpdatedAt, Id";

    // ORDER BY 句は SQL へそのまま展開されるため、呼び出し側の文字列を素通しさせない。
    // どの列を許可するか・一致しなかったときにどれを使うかはテーブルごとの都合なので、いずれも呼び出し側が指定する
    public static string NormalizeSort(string[] allowed, string defaultColumn, string? sort, bool desc)
    {
        ArgumentNullException.ThrowIfNull(allowed);
        var column = Array.IndexOf(allowed, sort) >= 0 ? sort! : defaultColumn;
        return desc ? $"{column} DESC" : column;
    }

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

    // 商品別売上の並び順
    public static string ProductSalesOrder(ProductSalesSort sort) =>
        sort == ProductSalesSort.Quantity ? "NetQuantity DESC, ProductCode" : "NetSales DESC, ProductCode";

    // SQLite の日時修飾子 ("+540 minutes" など)。UTC の列を店舗時刻にするときに使う
    public static string ToTimeZoneModifier(TimeZoneInfo timeZone, DateOnly date)
    {
        ArgumentNullException.ThrowIfNull(timeZone);
        var offset = timeZone.GetUtcOffset(date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc));
        return FormattableString.Invariant($"{(int)offset.TotalMinutes:+0;-0} minutes");
    }

    // テーブル作成後に増えた列を既存 DB に足す (CREATE TABLE IF NOT EXISTS では列は増えないため)
    public static async ValueTask<bool> EnsureColumnAsync(DbConnection con, string table, string column, string definition, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(con);

        if (await HasColumnAsync(con, table, column, cancellationToken).ConfigureAwait(false))
        {
            return false;
        }

#pragma warning disable CA2100
        await using var command = con.CreateCommand();
        command.CommandText = $"ALTER TABLE {table} ADD COLUMN {column} {definition}";
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
#pragma warning restore CA2100
        return true;
    }

    private static async ValueTask<bool> HasColumnAsync(DbConnection con, string table, string column, CancellationToken cancellationToken)
    {
#pragma warning disable CA2100
        await using var command = con.CreateCommand();
        command.CommandText = $"PRAGMA table_info({table})";
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
#pragma warning restore CA2100
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            if (String.Equals(reader.GetString(1), column, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
