namespace Pos.Server.Services;

using Pos.Server.Accessors;
using Pos.Server.Models;
using Pos.Server.Models.Entity;

public sealed class TerminalService
{
    private static readonly string[] SortColumns = ["TerminalNo", "Name", "UpdatedAt"];
    private const string DefaultSort = "TerminalNo";

    private readonly IDialect dialect;
    private readonly MasterAccessor masterAccessor;
    private readonly TimeProvider timeProvider;

    public TerminalService(
        IDialect dialect,
        MasterAccessor masterAccessor,
        TimeProvider timeProvider)
    {
        this.dialect = dialect;
        this.masterAccessor = masterAccessor;
        this.timeProvider = timeProvider;
    }

    public async ValueTask<PagedResult<TerminalEntity>> QueryPageAsync(Guid? storeId, DateTime? updatedSince, bool includeDeleted, string? sort, bool desc, int page, int size, CancellationToken cancellationToken)
    {
        var total = await masterAccessor.CountTerminalsAsync(storeId, updatedSince, includeDeleted, cancellationToken);
        var order = updatedSince is null ? SqlHelper.NormalizeSort(SortColumns, DefaultSort, sort, desc) : SqlHelper.SyncSort;
        var items = await masterAccessor.QueryTerminalListAsync(storeId, updatedSince, includeDeleted, order, size, page * size, cancellationToken);
        return new PagedResult<TerminalEntity>((int)total, page, size, items);
    }

    // 全件 (店舗、端末番号順)
    public ValueTask<List<TerminalEntity>> QueryAllAsync(bool includeDeleted, CancellationToken cancellationToken) =>
        masterAccessor.QueryTerminalAllAsync(includeDeleted, cancellationToken);

    public ValueTask<TerminalEntity?> QueryAsync(Guid id, CancellationToken cancellationToken) =>
        masterAccessor.QueryTerminalAsync(id, cancellationToken);

    public ValueTask<DataWriteStatus> InsertAsync(TerminalEntity entity, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(entity);

        var now = timeProvider.GetUtcNow().UtcDateTime;
        entity.Id = Guid.CreateVersion7();
        entity.CreatedAt = now;
        entity.UpdatedAt = now;
        entity.Version = 1;
        return ServiceHelper.InsertAsync(dialect, () => masterAccessor.InsertTerminalAsync(entity, cancellationToken));
    }

    public ValueTask<DataWriteStatus> UpdateAsync(TerminalEntity entity, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(entity);

        entity.UpdatedAt = timeProvider.GetUtcNow().UtcDateTime;
        return ServiceHelper.UpdateAsync(
            dialect,
            () => masterAccessor.UpdateTerminalAsync(entity.Id, entity.StoreId, entity.TerminalNo, entity.Name, entity.IsActive, entity.UpdatedAt, entity.Version, cancellationToken),
            async () => await masterAccessor.QueryTerminalAsync(entity.Id, cancellationToken) is { IsDeleted: false });
    }

    // 開設中シフトがある端末は削除できない
    public async ValueTask<DataWriteStatus> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        if (await masterAccessor.CountTerminalOpenShiftsAsync(id, cancellationToken) > 0)
        {
            return DataWriteStatus.InUse;
        }

        return await masterAccessor.DeleteTerminalAsync(id, timeProvider.GetUtcNow().UtcDateTime, cancellationToken) > 0 ? DataWriteStatus.Success : DataWriteStatus.NotFound;
    }
}
