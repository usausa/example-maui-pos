namespace Pos.Server.Services;

using Pos.Server.Accessors;
using Pos.Server.Models;
using Pos.Server.Models.Entity;

public sealed class StoreService
{
    private readonly IDialect dialect;
    private readonly MasterAccessor masterAccessor;
    private readonly InventoryAccessor inventoryAccessor;
    private readonly TimeProvider timeProvider;

    public StoreService(
        IDialect dialect,
        MasterAccessor masterAccessor,
        InventoryAccessor inventoryAccessor,
        TimeProvider timeProvider)
    {
        this.dialect = dialect;
        this.masterAccessor = masterAccessor;
        this.inventoryAccessor = inventoryAccessor;
        this.timeProvider = timeProvider;
    }

    // 店舗の TimeZone (IANA / Windows) を解決し、不明ならサーバのローカル
    public static TimeZoneInfo ResolveTimeZone(string? id) =>
        !String.IsNullOrEmpty(id) && TimeZoneInfo.TryFindSystemTimeZoneById(id, out var timeZone) ? timeZone : TimeZoneInfo.Local;

    // 差分同期 (updatedSince 指定時) は updatedAt, id 順 (SQL 側で固定)
    public async ValueTask<PagedResult<StoreEntity>> QueryPageAsync(DateTime? updatedSince, bool includeDeleted, StoreSort sort, bool desc, int page, int size, CancellationToken cancellationToken)
    {
        var total = await masterAccessor.CountStoresAsync(updatedSince, includeDeleted, cancellationToken);
        var items = await masterAccessor.QueryStoreListAsync(updatedSince, includeDeleted, sort, desc, size, page * size, cancellationToken);
        return new PagedResult<StoreEntity>((int)total, page, size, items);
    }

    // 全件 (コード順)
    public ValueTask<List<StoreEntity>> QueryAllAsync(bool includeDeleted, CancellationToken cancellationToken) =>
        masterAccessor.QueryStoreAllAsync(includeDeleted, cancellationToken);

    public ValueTask<StoreEntity?> QueryAsync(Guid id, CancellationToken cancellationToken) =>
        masterAccessor.QueryStoreAsync(id, cancellationToken);

    // Id / CreatedAt / UpdatedAt / Version はここで付与する
    public ValueTask<DataWriteStatus> InsertAsync(StoreEntity entity, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;
        entity.Id = Guid.CreateVersion7();
        entity.CreatedAt = now;
        entity.UpdatedAt = now;
        entity.Version = 1;
        return ServiceHelper.InsertAsync(dialect, () => masterAccessor.InsertStoreAsync(entity, cancellationToken));
    }

    public ValueTask<DataWriteResult<StoreEntity>> UpdateAsync(StoreEntity entity, CancellationToken cancellationToken)
    {
        entity.UpdatedAt = timeProvider.GetUtcNow().UtcDateTime;
        return ServiceHelper.UpdateAsync(
            dialect,
            () => masterAccessor.UpdateStoreAsync(
                entity.Id,
                entity.Code,
                entity.Name,
                entity.PostalCode,
                entity.Address,
                entity.Phone,
                entity.RegistrationNo,
                entity.ReceiptHeader,
                entity.ReceiptFooter,
                entity.TimeZone,
                entity.IsActive,
                entity.UpdatedAt,
                entity.Version,
                cancellationToken),
            async () => await masterAccessor.QueryStoreAsync(entity.Id, cancellationToken) is { IsDeleted: false });
    }

    // 端末・在庫が残っている店舗は削除できない
    public async ValueTask<DataWriteStatus> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        if ((await masterAccessor.CountTerminalsAsync(id, null, false, cancellationToken) > 0) ||
            (await inventoryAccessor.CountLevelsAsync(id, null, null, false, null, cancellationToken) > 0))
        {
            return DataWriteStatus.InUse;
        }

        return await masterAccessor.DeleteStoreAsync(id, timeProvider.GetUtcNow().UtcDateTime, cancellationToken) > 0 ? DataWriteStatus.Success : DataWriteStatus.NotFound;
    }
}
