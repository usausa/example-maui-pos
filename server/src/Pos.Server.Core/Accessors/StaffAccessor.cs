namespace Pos.Server.Accessors;

using Pos.Server.Infrastructure.Data;
using Pos.Server.Models.Entity;

[DataAccessor]
[ExecuteConfig(typeof(DataProfile))]
public sealed partial class StaffAccessor
{
    [Execute]
    public partial void Create();

    // storeId 指定時は本部 (StoreId = NULL) も含める
    [ExecuteScalar]
    public partial ValueTask<long> CountAsync(Guid? storeId, DateTime? updatedSince, bool includeDeleted, CancellationToken cancellationToken);

    [Query]
    public partial ValueTask<List<StaffEntity>> QueryListAsync(Guid? storeId, DateTime? updatedSince, bool includeDeleted, string sort, int limit, int offset, CancellationToken cancellationToken);

    [QueryFirst]
    [SelectSingle(typeof(StaffEntity), Table = "Staff")]
    public partial ValueTask<StaffEntity?> QueryAsync(Guid id, CancellationToken cancellationToken);

    [Execute]
    [Insert(typeof(StaffEntity), Table = "Staff")]
    public partial ValueTask<int> InsertAsync(StaffEntity entity, CancellationToken cancellationToken);

    [Execute]
    public partial ValueTask<int> UpdateAsync(
        Guid id,
        string code,
        string name,
        StaffRole role,
        Guid? storeId,
        bool isActive,
        DateTime updatedAt,
        int version,
        CancellationToken cancellationToken);

    [Execute]
    public partial ValueTask<int> DeleteAsync(Guid id, DateTime updatedAt, CancellationToken cancellationToken);
}
