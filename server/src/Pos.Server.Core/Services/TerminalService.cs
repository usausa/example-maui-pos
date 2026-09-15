namespace Pos.Server.Services;

using Pos.Server.Accessors;
using Pos.Server.Models;
using Pos.Server.Models.Entity;

public sealed class TerminalService
{
    // 最終通信からこの時間内なら通信中とみなす
    private static readonly TimeSpan OnlineThreshold = TimeSpan.FromMinutes(5);

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

    public async ValueTask<PagedResult<TerminalEntity>> QueryPageAsync(Guid? storeId, DateTime? updatedSince, bool includeDeleted, TerminalSort sort, bool desc, int page, int size, CancellationToken cancellationToken)
    {
        var total = await masterAccessor.CountTerminalsAsync(storeId, updatedSince, includeDeleted, cancellationToken);
        var items = await masterAccessor.QueryTerminalListAsync(storeId, updatedSince, includeDeleted, sort, desc, size, page * size, cancellationToken);
        return new PagedResult<TerminalEntity>((int)total, page, size, items);
    }

    // 全件 (店舗、端末番号順)
    public ValueTask<List<TerminalEntity>> QueryAllAsync(bool includeDeleted, CancellationToken cancellationToken) =>
        masterAccessor.QueryTerminalAllAsync(includeDeleted, cancellationToken);

    public ValueTask<TerminalEntity?> QueryAsync(Guid id, CancellationToken cancellationToken) =>
        masterAccessor.QueryTerminalAsync(id, cancellationToken);

    public bool IsOnline(DateTime? lastSeenAt) =>
        (lastSeenAt is not null) && (timeProvider.GetUtcNow().UtcDateTime - lastSeenAt.Value < OnlineThreshold);

    public ValueTask<DataWriteStatus> InsertAsync(TerminalEntity entity, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;
        entity.Id = Guid.CreateVersion7();
        entity.CreatedAt = now;
        entity.UpdatedAt = now;
        entity.Version = 1;
        return ServiceHelper.InsertAsync(dialect, () => masterAccessor.InsertTerminalAsync(entity, cancellationToken));
    }

    public ValueTask<DataWriteResult<TerminalEntity>> UpdateAsync(TerminalEntity entity, CancellationToken cancellationToken)
    {
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
