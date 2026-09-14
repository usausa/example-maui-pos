namespace Pos.Server.Services;

using Pos.Server.Accessors;
using Pos.Server.Models.Entity;

// 値引 (少数なのでページングなし)
public sealed class DiscountService
{
    private readonly IDialect dialect;
    private readonly MasterAccessor masterAccessor;
    private readonly TimeProvider timeProvider;

    public DiscountService(
        IDialect dialect,
        MasterAccessor masterAccessor,
        TimeProvider timeProvider)
    {
        this.dialect = dialect;
        this.masterAccessor = masterAccessor;
        this.timeProvider = timeProvider;
    }

    public ValueTask<List<DiscountEntity>> QueryListAsync(DateTime? updatedSince, bool includeDeleted, CancellationToken cancellationToken) =>
        masterAccessor.QueryDiscountListAsync(updatedSince, includeDeleted, cancellationToken);

    public ValueTask<DiscountEntity?> QueryAsync(Guid id, CancellationToken cancellationToken) =>
        masterAccessor.QueryDiscountAsync(id, cancellationToken);

    public ValueTask<DataWriteStatus> InsertAsync(DiscountEntity entity, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(entity);

        var now = timeProvider.GetUtcNow().UtcDateTime;
        entity.Id = Guid.CreateVersion7();
        entity.CreatedAt = now;
        entity.UpdatedAt = now;
        entity.Version = 1;
        return ServiceHelper.InsertAsync(dialect, () => masterAccessor.InsertDiscountAsync(entity, cancellationToken));
    }

    public ValueTask<DataWriteStatus> UpdateAsync(DiscountEntity entity, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(entity);

        entity.UpdatedAt = timeProvider.GetUtcNow().UtcDateTime;
        return ServiceHelper.UpdateAsync(
            dialect,
            () => masterAccessor.UpdateDiscountAsync(
                entity.Id,
                entity.Code,
                entity.Name,
                entity.Type,
                entity.Value,
                entity.Scope,
                entity.RequiresApproval,
                entity.IsActive,
                entity.SortOrder,
                entity.UpdatedAt,
                entity.Version,
                cancellationToken),
            async () => await masterAccessor.QueryDiscountAsync(entity.Id, cancellationToken) is { IsDeleted: false });
    }

    public async ValueTask<DataWriteStatus> DeleteAsync(Guid id, CancellationToken cancellationToken) =>
        await masterAccessor.DeleteDiscountAsync(id, timeProvider.GetUtcNow().UtcDateTime, cancellationToken) > 0 ? DataWriteStatus.Success : DataWriteStatus.NotFound;
}
