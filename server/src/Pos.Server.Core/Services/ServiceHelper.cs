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

    // 更新後の行が返らなければ、行がなければ (削除済みを含む) NotFound、あれば VersionMismatch
    public static async ValueTask<DataWriteResult<T>> UpdateAsync<T>(IDialect dialect, Func<ValueTask<T?>> update, Func<ValueTask<bool>> exists)
        where T : class
    {
        try
        {
            var entity = await update();
            if (entity is not null)
            {
                return new DataWriteResult<T>(DataWriteStatus.Success, entity);
            }
        }
        catch (DbException ex) when (dialect.IsDuplicate(ex))
        {
            return new DataWriteResult<T>(DataWriteStatus.Duplicate, null);
        }

        return new DataWriteResult<T>(await exists() ? DataWriteStatus.VersionMismatch : DataWriteStatus.NotFound, null);
    }

    // 部分一致検索の LIKE パターン (% _ はエスケープ)
    public static string? ToLikePattern(IDialect dialect, string? keyword) =>
        String.IsNullOrWhiteSpace(keyword) ? null : $"%{dialect.LikeEscape(keyword.Trim())}%";
}
