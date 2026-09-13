namespace Pos.Server.Accessors;

using Pos.Server.Infrastructure.Data;
using Pos.Server.Models.Entity;

[DataAccessor]
[ExecuteConfig(typeof(DataProfile))]
public sealed partial class StoreAccessor
{
    [Execute]
    public partial void Create();

    [ExecuteScalar]
    public partial ValueTask<long> CountAsync(DateTime? updatedSince, bool includeDeleted, CancellationToken cancellationToken);

    [Query]
    public partial ValueTask<List<StoreEntity>> QueryListAsync(DateTime? updatedSince, bool includeDeleted, string sort, int limit, int offset, CancellationToken cancellationToken);

    [QueryFirst]
    [SelectSingle(typeof(StoreEntity), Table = "Stores")]
    public partial ValueTask<StoreEntity?> QueryAsync(Guid id, CancellationToken cancellationToken);

    [Execute]
    [Insert(typeof(StoreEntity), Table = "Stores")]
    public partial ValueTask<int> InsertAsync(StoreEntity entity, CancellationToken cancellationToken);

    // Version が一致する行だけ更新する (楽観ロック)。戻り値 0 = 競合または削除済み
    [Execute]
    public partial ValueTask<int> UpdateAsync(
        Guid id,
        string code,
        string name,
        string? postalCode,
        string? address,
        string? phone,
        string? registrationNo,
        string? receiptHeader,
        string? receiptFooter,
        string timeZone,
        bool isActive,
        DateTime updatedAt,
        int version,
        CancellationToken cancellationToken);

    [Execute]
    public partial ValueTask<int> DeleteAsync(Guid id, DateTime updatedAt, CancellationToken cancellationToken);
}
