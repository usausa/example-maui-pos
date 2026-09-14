namespace Pos.Terminal.Helpers.Data;

using System.Data.Common;

// テーブル作成後に増えた列を既存のローカル DB に足す (CREATE TABLE IF NOT EXISTS では列は増えないため)
public static class SchemaHelper
{
    public static async ValueTask<bool> EnsureColumnAsync(DbConnection con, string table, string column, string definition)
    {
        if (await HasColumnAsync(con, table, column))
        {
            return false;
        }

#pragma warning disable CA2100
        await using var command = con.CreateCommand();
        command.CommandText = $"ALTER TABLE {table} ADD COLUMN {column} {definition}";
        await command.ExecuteNonQueryAsync();
#pragma warning restore CA2100
        return true;
    }

    private static async ValueTask<bool> HasColumnAsync(DbConnection con, string table, string column)
    {
#pragma warning disable CA2100
        await using var command = con.CreateCommand();
        command.CommandText = $"PRAGMA table_info({table})";
        await using var reader = await command.ExecuteReaderAsync();
#pragma warning restore CA2100
        while (await reader.ReadAsync())
        {
            if (String.Equals(reader.GetString(1), column, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
