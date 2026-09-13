namespace Pos.Server.Accessors;

using Pos.Server.Infrastructure.Data;
using Pos.Server.Models.Entity;

[DataAccessor]
[ExecuteConfig(typeof(DataProfile))]
public sealed partial class CategoryAccessor
{
    [Execute]
    public partial void Create();

    [ExecuteScalar]
    public partial ValueTask<long> CountAsync(DateTime? updatedSince, bool includeDeleted, CancellationToken cancellationToken);

    [Query]
    public partial ValueTask<List<CategoryEntity>> QueryListAsync(DateTime? updatedSince, bool includeDeleted, string sort, int limit, int offset, CancellationToken cancellationToken);

    [QueryFirst]
    [SelectSingle(typeof(CategoryEntity), Table = "Categories")]
    public partial ValueTask<CategoryEntity?> QueryAsync(Guid id, CancellationToken cancellationToken);

    [Execute]
    [Insert(typeof(CategoryEntity), Table = "Categories")]
    public partial ValueTask<int> InsertAsync(CategoryEntity entity, CancellationToken cancellationToken);

    [Execute]
    public partial ValueTask<int> UpdateAsync(
        Guid id,
        string code,
        string name,
        Guid? parentId,
        int sortOrder,
        DateTime updatedAt,
        int version,
        CancellationToken cancellationToken);

    [Execute]
    public partial ValueTask<int> DeleteAsync(Guid id, DateTime updatedAt, CancellationToken cancellationToken);

    // 削除可否 (所属商品・子部門があれば IN_USE)
    [ExecuteScalar]
    public partial ValueTask<long> CountProductsAsync(Guid categoryId, CancellationToken cancellationToken);

    [ExecuteScalar]
    public partial ValueTask<long> CountChildrenAsync(Guid parentId, CancellationToken cancellationToken);
}
