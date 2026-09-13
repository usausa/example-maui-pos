namespace Pos.Server.Accessors;

using Pos.Server.Infrastructure.Data;
using Pos.Server.Models;
using Pos.Server.Models.Entity;

[DataAccessor]
[ExecuteConfig(typeof(DataProfile))]
public sealed partial class InventoryAccessor
{
    [Execute]
    public partial void Create();

    //--------------------------------------------------------------------------------
    // Level
    //--------------------------------------------------------------------------------

    [ExecuteScalar]
    public partial ValueTask<long> CountLevelsAsync(Guid? storeId, Guid? productId, Guid? categoryId, bool negativeOnly, DateTime? updatedSince, CancellationToken cancellationToken);

    [Query]
    public partial ValueTask<List<InventoryLevelEntity>> QueryLevelListAsync(Guid? storeId, Guid? productId, Guid? categoryId, bool negativeOnly, DateTime? updatedSince, string sort, int limit, int offset, CancellationToken cancellationToken);

    [QueryFirst]
    [SelectSingle(typeof(InventoryLevelEntity), Table = "InventoryLevels")]
    public partial ValueTask<InventoryLevelEntity?> QueryLevelAsync(DbTransaction tx, Guid storeId, Guid productId, CancellationToken cancellationToken);

    // 商品の全店舗在庫 (他店在庫照会)
    [Query]
    public partial ValueTask<List<ProductInventoryLevel>> QueryLevelsByProductAsync(Guid productId, CancellationToken cancellationToken);

    // UPSERT で加減算し、更新後の数量を返す
    [ExecuteScalar]
    public partial ValueTask<decimal> AddQuantityAsync(DbTransaction tx, Guid storeId, Guid productId, decimal delta, DateTime updatedAt, CancellationToken cancellationToken);

    //--------------------------------------------------------------------------------
    // Change
    //--------------------------------------------------------------------------------

    [Execute]
    [Insert(typeof(InventoryChangeEntity), Table = "InventoryChanges")]
    public partial ValueTask<int> InsertChangeAsync(DbTransaction tx, InventoryChangeEntity entity, CancellationToken cancellationToken);

    [QueryFirst]
    [SelectSingle(typeof(InventoryChangeEntity), Table = "InventoryChanges")]
    public partial ValueTask<InventoryChangeEntity?> QueryChangeAsync(Guid id, CancellationToken cancellationToken);

    // from / to は UTC 日時 (to は含まない)
    [ExecuteScalar]
    public partial ValueTask<long> CountChangesAsync(Guid? storeId, Guid? productId, InventoryChangeType? type, DateTime? from, DateTime? to, CancellationToken cancellationToken);

    [Query]
    public partial ValueTask<List<InventoryChangeEntity>> QueryChangeListAsync(Guid? storeId, Guid? productId, InventoryChangeType? type, DateTime? from, DateTime? to, int limit, int offset, CancellationToken cancellationToken);
}
