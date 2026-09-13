namespace Pos.Server.Accessors;

using Pos.Server.Infrastructure.Data;
using Pos.Server.Models.Entity;

[DataAccessor]
[ExecuteConfig(typeof(DataProfile))]
public sealed partial class AdjustmentReasonAccessor
{
    [Execute]
    public partial void Create();

    [Query]
    public partial ValueTask<List<AdjustmentReasonEntity>> QueryListAsync(DateTime? updatedSince, bool includeDeleted, CancellationToken cancellationToken);

    [QueryFirst]
    [SelectSingle(typeof(AdjustmentReasonEntity), Table = "AdjustmentReasons")]
    public partial ValueTask<AdjustmentReasonEntity?> QueryAsync(Guid id, CancellationToken cancellationToken);

    [Execute]
    [Insert(typeof(AdjustmentReasonEntity), Table = "AdjustmentReasons")]
    public partial ValueTask<int> InsertAsync(AdjustmentReasonEntity entity, CancellationToken cancellationToken);

    [Execute]
    public partial ValueTask<int> UpdateAsync(
        Guid id,
        string code,
        string name,
        int sortOrder,
        bool isActive,
        DateTime updatedAt,
        int version,
        CancellationToken cancellationToken);

    [Execute]
    public partial ValueTask<int> DeleteAsync(Guid id, DateTime updatedAt, CancellationToken cancellationToken);
}
