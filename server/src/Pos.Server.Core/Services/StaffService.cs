namespace Pos.Server.Services;

using Pos.Server.Accessors;
using Pos.Server.Models;
using Pos.Server.Models.Entity;

public sealed class StaffService
{
    private static readonly string[] SortColumns = ["Code", "Name", "UpdatedAt"];
    private const string DefaultSort = "Code";

    private readonly IDialect dialect;
    private readonly MasterAccessor masterAccessor;
    private readonly TimeProvider timeProvider;

    public StaffService(
        IDialect dialect,
        MasterAccessor masterAccessor,
        TimeProvider timeProvider)
    {
        this.dialect = dialect;
        this.masterAccessor = masterAccessor;
        this.timeProvider = timeProvider;
    }

    // storeId 指定時は本部 (StoreId = NULL) も含める
    public async ValueTask<PagedResult<StaffEntity>> QueryPageAsync(Guid? storeId, DateTime? updatedSince, bool includeDeleted, string? sort, bool desc, int page, int size, CancellationToken cancellationToken)
    {
        var total = await masterAccessor.CountStaffAsync(storeId, updatedSince, includeDeleted, cancellationToken);
        var order = updatedSince is null ? SqlHelper.NormalizeSort(SortColumns, DefaultSort, sort, desc) : SqlHelper.SyncSort;
        var items = await masterAccessor.QueryStaffListAsync(storeId, updatedSince, includeDeleted, order, size, page * size, cancellationToken);
        return new PagedResult<StaffEntity>((int)total, page, size, items);
    }

    // 全件 (コード順)
    public ValueTask<List<StaffEntity>> QueryAllAsync(bool includeDeleted, CancellationToken cancellationToken) =>
        masterAccessor.QueryStaffAllAsync(includeDeleted, cancellationToken);

    public ValueTask<StaffEntity?> QueryAsync(Guid id, CancellationToken cancellationToken) =>
        masterAccessor.QueryStaffAsync(id, cancellationToken);

    public ValueTask<DataWriteStatus> InsertAsync(StaffEntity entity, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(entity);

        var now = timeProvider.GetUtcNow().UtcDateTime;
        entity.Id = Guid.CreateVersion7();
        entity.CreatedAt = now;
        entity.UpdatedAt = now;
        entity.Version = 1;
        return ServiceHelper.InsertAsync(dialect, () => masterAccessor.InsertStaffAsync(entity, cancellationToken));
    }

    public ValueTask<DataWriteStatus> UpdateAsync(StaffEntity entity, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(entity);

        entity.UpdatedAt = timeProvider.GetUtcNow().UtcDateTime;
        return ServiceHelper.UpdateAsync(
            dialect,
            () => masterAccessor.UpdateStaffAsync(entity.Id, entity.Code, entity.Name, entity.Role, entity.StoreId, entity.IsActive, entity.UpdatedAt, entity.Version, cancellationToken),
            async () => await masterAccessor.QueryStaffAsync(entity.Id, cancellationToken) is { IsDeleted: false });
    }

    public async ValueTask<DataWriteStatus> DeleteAsync(Guid id, CancellationToken cancellationToken) =>
        await masterAccessor.DeleteStaffAsync(id, timeProvider.GetUtcNow().UtcDateTime, cancellationToken) > 0 ? DataWriteStatus.Success : DataWriteStatus.NotFound;
}
