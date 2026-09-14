namespace Pos.Server.Services;

internal static class ServiceHelper
{
    // 一意制約違反は Duplicate
    public static async ValueTask<DataWriteStatus> InsertAsync(IDialect dialect, Func<ValueTask<int>> insert)
    {
        try
        {
            await insert();
            return DataWriteStatus.Success;
        }
        catch (DbException ex) when (dialect.IsDuplicate(ex))
        {
            return DataWriteStatus.Duplicate;
        }
    }

    // 更新 0 件は、行がなければ (削除済みを含む) NotFound、あれば VersionMismatch
    public static async ValueTask<DataWriteStatus> UpdateAsync(IDialect dialect, Func<ValueTask<int>> update, Func<ValueTask<bool>> exists)
    {
        try
        {
            if (await update() > 0)
            {
                return DataWriteStatus.Success;
            }
        }
        catch (DbException ex) when (dialect.IsDuplicate(ex))
        {
            return DataWriteStatus.Duplicate;
        }

        return await exists() ? DataWriteStatus.VersionMismatch : DataWriteStatus.NotFound;
    }

    // 部分一致検索の LIKE パターン (% _ はエスケープ)
    public static string? ToLikePattern(IDialect dialect, string? keyword) =>
        String.IsNullOrWhiteSpace(keyword) ? null : $"%{dialect.LikeEscape(keyword.Trim())}%";
}
