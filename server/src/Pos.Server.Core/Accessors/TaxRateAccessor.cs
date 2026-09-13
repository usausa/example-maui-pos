namespace Pos.Server.Accessors;

using Pos.Server.Infrastructure.Data;
using Pos.Server.Models.Entity;

[DataAccessor]
[ExecuteConfig(typeof(DataProfile))]
public sealed partial class TaxRateAccessor
{
    [Execute]
    public partial void Create();

    // 少数なのでページングなし (SortOrder, Code 順)
    [Query]
    public partial ValueTask<List<TaxRateEntity>> QueryListAsync(DateTime? updatedSince, bool includeDeleted, CancellationToken cancellationToken);

    [QueryFirst]
    [SelectSingle(typeof(TaxRateEntity), Table = "TaxRates")]
    public partial ValueTask<TaxRateEntity?> QueryAsync(Guid id, CancellationToken cancellationToken);

    [Execute]
    [Insert(typeof(TaxRateEntity), Table = "TaxRates")]
    public partial ValueTask<int> InsertAsync(TaxRateEntity entity, CancellationToken cancellationToken);

    [Execute]
    public partial ValueTask<int> UpdateAsync(
        Guid id,
        string code,
        string name,
        decimal rate,
        TaxKind kind,
        bool isDefault,
        int sortOrder,
        DateTime updatedAt,
        int version,
        CancellationToken cancellationToken);

    [Execute]
    public partial ValueTask<int> DeleteAsync(Guid id, DateTime updatedAt, CancellationToken cancellationToken);

    // 既定は 1 件だけ: 指定 ID 以外の IsDefault を落とす
    [Execute]
    public partial ValueTask<int> ClearDefaultAsync(Guid exceptId, DateTime updatedAt, CancellationToken cancellationToken);

    // 削除可否 (使用中商品があれば IN_USE)
    [ExecuteScalar]
    public partial ValueTask<long> CountProductsAsync(Guid taxRateId, CancellationToken cancellationToken);
}
