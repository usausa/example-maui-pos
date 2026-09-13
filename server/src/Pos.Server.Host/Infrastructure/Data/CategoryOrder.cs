namespace Pos.Server.Host.Infrastructure.Data;

using Pos.Server.Models.Entity;

internal static class CategoryOrder
{
    // 大分類の並び順に、その中分類を続ける (セレクタ・ツリー用)。親のない中分類は末尾
    public static List<CategoryEntity> Sort(IReadOnlyList<CategoryEntity> categories)
    {
        ArgumentNullException.ThrowIfNull(categories);

        var result = new List<CategoryEntity>(categories.Count);
        foreach (var parent in categories.Where(static x => x.ParentId is null).OrderBy(static x => x.SortOrder).ThenBy(static x => x.Code, StringComparer.Ordinal))
        {
            result.Add(parent);
            result.AddRange(categories.Where(x => x.ParentId == parent.Id).OrderBy(static x => x.SortOrder).ThenBy(static x => x.Code, StringComparer.Ordinal));
        }

        result.AddRange(categories.Where(x => !result.Contains(x)).OrderBy(static x => x.SortOrder));
        return result;
    }
}
