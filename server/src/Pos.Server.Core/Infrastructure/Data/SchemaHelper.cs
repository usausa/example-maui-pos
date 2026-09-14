namespace Pos.Server.Infrastructure.Data;

using System.Data.Common;

// テーブル作成後に増えた列を既存 DB に足す (CREATE TABLE IF NOT EXISTS では列は増えないため)
public static class SchemaHelper
{
    public static async ValueTask<bool> EnsureColumnAsync(DbConnection con, string table, string column, string definition, CancellationToken cancellationToken)
    {
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
