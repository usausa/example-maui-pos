namespace Pos.Server.Services;

using Pos.Server.Accessors;
using Pos.Server.Models;
using Pos.Server.Models.Entity;
using Pos.Server.Models.Parameters;

public sealed class TerminalService
{
    // 最終通信からこの時間内なら通信中とみなす
    private static readonly TimeSpan OnlineThreshold = TimeSpan.FromMinutes(5);

    private readonly TimeProvider timeProvider;
    private readonly IDialect dialect;
    private readonly MasterAccessor masterAccessor;

    public TerminalService(
        TimeProvider timeProvider,
        IDialect dialect,
        MasterAccessor masterAccessor)
    {
        this.timeProvider = timeProvider;
        this.dialect = dialect;
        this.masterAccessor = masterAccessor;
    }

    public async ValueTask<PagedResult<TerminalEntity>> QueryPageAsync(TerminalQueryParameter parameter, CancellationToken cancellationToken)
    {
        var total = await masterAccessor.CountTerminalsAsync(parameter.StoreId, parameter.UpdatedSince, parameter.IncludeDeleted, cancellationToken);
        var items = await masterAccessor.QueryTerminalListAsync(parameter.StoreId, parameter.UpdatedSince, parameter.IncludeDeleted, parameter.Sort, parameter.Desc, parameter.Size, parameter.Page * parameter.Size, cancellationToken);
        return new PagedResult<TerminalEntity>((int)total, parameter.Page, parameter.Size, items);
    }

    // 全件 (店舗、端末番号順)
    public ValueTask<List<TerminalEntity>> QueryAllAsync(bool includeDeleted, CancellationToken cancellationToken) =>
        masterAccessor.QueryTerminalAllAsync(includeDeleted, cancellationToken);

    public ValueTask<TerminalEntity?> QueryAsync(Guid id, CancellationToken cancellationToken) =>
        masterAccessor.QueryTerminalAsync(id, cancellationToken);

    public bool IsOnline(DateTime? lastSeenAt) =>
        (lastSeenAt is not null) && (timeProvider.GetUtcNow().UtcDateTime - lastSeenAt.Value < OnlineThreshold);

    // 端末からの通信 (heartbeat) を記録する
    public ValueTask<int> UpdateSeenAsync(Guid id, string? appVersion, CancellationToken cancellationToken) =>
        masterAccessor.UpdateTerminalSeenAsync(id, timeProvider.GetUtcNow().UtcDateTime, appVersion, cancellationToken);

    // 登録・更新は、店舗がなければ Invalid
    public async ValueTask<DataWriteStatus> InsertAsync(TerminalEntity entity, CancellationToken cancellationToken)
    {
        if (!await ExistsStoreAsync(entity.StoreId, cancellationToken))
        {
            return DataWriteStatus.Invalid;
        }

        var now = timeProvider.GetUtcNow().UtcDateTime;
        entity.Id = Guid.CreateVersion7();
        entity.CreatedAt = now;
        entity.UpdatedAt = now;
        entity.Version = 1;
        return await ServiceHelper.InsertAsync(dialect, () => masterAccessor.InsertTerminalAsync(entity, cancellationToken));
    }

    public async ValueTask<DataWriteResult<TerminalEntity>> UpdateAsync(TerminalEntity entity, CancellationToken cancellationToken)
    {
        if (!await ExistsStoreAsync(entity.StoreId, cancellationToken))
        {
            return new DataWriteResult<TerminalEntity>(DataWriteStatus.Invalid, null);
        }

        entity.UpdatedAt = timeProvider.GetUtcNow().UtcDateTime;
        return await ServiceHelper.UpdateAsync(
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

    // 参照先は書く前に確かめ、削除済みはないものとする (削除済みを指すと、削除の側の使用中の確かめが崩れる)
    private async ValueTask<bool> ExistsStoreAsync(Guid storeId, CancellationToken cancellationToken) =>
        await masterAccessor.QueryStoreAsync(storeId, cancellationToken) is { IsDeleted: false };
}
