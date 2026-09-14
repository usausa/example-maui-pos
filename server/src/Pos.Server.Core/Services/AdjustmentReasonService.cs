namespace Pos.Server.Services;

using Pos.Server.Accessors;
using Pos.Server.Models.Entity;

// 在庫調整理由 (少数なのでページングなし)
public sealed class AdjustmentReasonService
{
    private readonly IDialect dialect;
    private readonly MasterAccessor masterAccessor;
    private readonly TimeProvider timeProvider;

    public AdjustmentReasonService(
        IDialect dialect,
        MasterAccessor masterAccessor,
        TimeProvider timeProvider)
    {
        this.dialect = dialect;
        this.masterAccessor = masterAccessor;
        this.timeProvider = timeProvider;
    }

    public ValueTask<List<AdjustmentReasonEntity>> QueryListAsync(DateTime? updatedSince, bool includeDeleted, CancellationToken cancellationToken) =>
        masterAccessor.QueryAdjustmentReasonListAsync(updatedSince, includeDeleted, cancellationToken);

    public ValueTask<AdjustmentReasonEntity?> QueryAsync(Guid id, CancellationToken cancellationToken) =>
        masterAccessor.QueryAdjustmentReasonAsync(id, cancellationToken);

    public ValueTask<DataWriteStatus> InsertAsync(AdjustmentReasonEntity entity, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(entity);

        var now = timeProvider.GetUtcNow().UtcDateTime;
        entity.Id = Guid.CreateVersion7();
        entity.CreatedAt = now;
        entity.UpdatedAt = now;
        entity.Version = 1;
        return ServiceHelper.InsertAsync(dialect, () => masterAccessor.InsertAdjustmentReasonAsync(entity, cancellationToken));
    }

    public ValueTask<DataWriteStatus> UpdateAsync(AdjustmentReasonEntity entity, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(entity);

        entity.UpdatedAt = timeProvider.GetUtcNow().UtcDateTime;
        return ServiceHelper.UpdateAsync(
            dialect,
            () => masterAccessor.UpdateAdjustmentReasonAsync(entity.Id, entity.Code, entity.Name, entity.SortOrder, entity.IsActive, entity.UpdatedAt, entity.Version, cancellationToken),
            async () => await masterAccessor.QueryAdjustmentReasonAsync(entity.Id, cancellationToken) is { IsDeleted: false });
    }

    public async ValueTask<DataWriteStatus> DeleteAsync(Guid id, CancellationToken cancellationToken) =>
        await masterAccessor.DeleteAdjustmentReasonAsync(id, timeProvider.GetUtcNow().UtcDateTime, cancellationToken) > 0 ? DataWriteStatus.Success : DataWriteStatus.NotFound;
}
