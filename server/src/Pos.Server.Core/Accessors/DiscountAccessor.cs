namespace Pos.Server.Accessors;

using Pos.Server.Infrastructure.Data;
using Pos.Server.Models.Entity;

[DataAccessor]
[ExecuteConfig(typeof(DataProfile))]
public sealed partial class DiscountAccessor
{
    [Execute]
    public partial void Create();

    [Query]
    public partial ValueTask<List<DiscountEntity>> QueryListAsync(DateTime? updatedSince, bool includeDeleted, CancellationToken cancellationToken);

    [QueryFirst]
    [SelectSingle(typeof(DiscountEntity), Table = "Discounts")]
    public partial ValueTask<DiscountEntity?> QueryAsync(Guid id, CancellationToken cancellationToken);

    [Execute]
    [Insert(typeof(DiscountEntity), Table = "Discounts")]
    public partial ValueTask<int> InsertAsync(DiscountEntity entity, CancellationToken cancellationToken);

    [Execute]
    public partial ValueTask<int> UpdateAsync(
        Guid id,
        string code,
        string name,
        DiscountType type,
        decimal value,
        DiscountScope scope,
        bool requiresApproval,
        bool isActive,
        int sortOrder,
        DateTime updatedAt,
        int version,
        CancellationToken cancellationToken);

    [Execute]
    public partial ValueTask<int> DeleteAsync(Guid id, DateTime updatedAt, CancellationToken cancellationToken);
}
