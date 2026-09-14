namespace Pos.Server.Accessors;

using Pos.Server.Infrastructure.Data;
using Pos.Server.Models.Entity;

[DataAccessor]
[ExecuteConfig(typeof(DataProfile))]
public sealed partial class PaymentMethodAccessor
{
    [Execute]
    public partial void Create();

    [Query]
    public partial ValueTask<List<PaymentMethodEntity>> QueryListAsync(DateTime? updatedSince, bool includeDeleted, CancellationToken cancellationToken);

    [QueryFirst]
    [SelectSingle(typeof(PaymentMethodEntity), Table = "PaymentMethods")]
    public partial ValueTask<PaymentMethodEntity?> QueryAsync(Guid id, CancellationToken cancellationToken);

    [Execute]
    [Insert(typeof(PaymentMethodEntity), Table = "PaymentMethods")]
    public partial ValueTask<int> InsertAsync(PaymentMethodEntity entity, CancellationToken cancellationToken);

    [Execute]
    public partial ValueTask<int> UpdateAsync(
        Guid id,
        string code,
        string name,
        string? shortName,
        PaymentKind kind,
        bool allowsChange,
        bool requiresReference,
        bool isActive,
        int sortOrder,
        DateTime updatedAt,
        int version,
        CancellationToken cancellationToken);

    [Execute]
    public partial ValueTask<int> DeleteAsync(Guid id, DateTime updatedAt, CancellationToken cancellationToken);

    // Kind = Points かつ有効な行はちょうど 1 件 (指定 ID を除いた件数)
    [ExecuteScalar]
    public partial ValueTask<long> CountActivePointsAsync(Guid exceptId, CancellationToken cancellationToken);
}
