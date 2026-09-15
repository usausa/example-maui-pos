namespace Pos.Terminal.Helpers.Data;

// SQL の値の組み立て
public static class SqlHelper
{
    // 部分一致の LIKE パターン (\ % _ はエスケープ。SQL 側は ESCAPE '\')
    public static string? ToLikePattern(string? keyword) =>
        String.IsNullOrEmpty(keyword)
            ? null
            : "%" + keyword.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("%", "\\%", StringComparison.Ordinal).Replace("_", "\\_", StringComparison.Ordinal) + "%";
}
