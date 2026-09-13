namespace Pos.Server.Host.Infrastructure.Api;

using Smart.Data;

public static class ApiHelper
{
    public const int DefaultPageSize = 20;

    public const int MaxPageSize = 1000;

    // 差分同期 (updatedSince 指定時) は updatedAt, id 昇順で固定 (api-design §2.2)
    private const string SyncSort = "UpdatedAt, Id";

    public static string ResolveSort(string[] allowed, string defaultColumn, string? sort, bool desc, DateTime? updatedSince) =>
        updatedSince is null ? SqlHelper.NormalizeSort(allowed, defaultColumn, sort, desc) : SyncSort;

    // 部分一致検索の LIKE パターン (% _ はエスケープ)
    public static string? ToLikePattern(IDialect dialect, string? keyword) =>
        String.IsNullOrWhiteSpace(keyword) ? null : $"%{dialect.LikeEscape(keyword.Trim())}%";
}
