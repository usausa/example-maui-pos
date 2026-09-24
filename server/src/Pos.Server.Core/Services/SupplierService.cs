namespace Pos.Server.Services;

using Pos.Server.Accessors;
using Pos.Server.Models.Entity;

// 仕入先 (少数なのでページングなし)。削除は論理削除で、登録済みの入荷は名前を引ける
public sealed class SupplierService
{
    private readonly TimeProvider timeProvider;
    private readonly IDialect dialect;
    private readonly MasterAccessor masterAccessor;

    public SupplierService(
        TimeProvider timeProvider,
        IDialect dialect,
        MasterAccessor masterAccessor)
    {
        this.timeProvider = timeProvider;
        this.dialect = dialect;
        this.masterAccessor = masterAccessor;
    }

    public ValueTask<List<SupplierEntity>> QueryListAsync(bool includeDeleted, CancellationToken cancellationToken) =>
        masterAccessor.QuerySupplierListAsync(includeDeleted, cancellationToken);

    public ValueTask<SupplierEntity?> QueryAsync(Guid id, CancellationToken cancellationToken) =>
        masterAccessor.QuerySupplierAsync(id, cancellationToken);

    public ValueTask<DataWriteStatus> InsertAsync(SupplierEntity entity, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;
        entity.Id = Guid.CreateVersion7();
        entity.CreatedAt = now;
        entity.UpdatedAt = now;
        entity.Version = 1;
        return ServiceHelper.InsertAsync(dialect, () => masterAccessor.InsertSupplierAsync(entity, cancellationToken));
    }

    public ValueTask<DataWriteResult<SupplierEntity>> UpdateAsync(SupplierEntity entity, CancellationToken cancellationToken)
    {
        entity.UpdatedAt = timeProvider.GetUtcNow().UtcDateTime;
        return ServiceHelper.UpdateAsync(
            dialect,
            () => masterAccessor.UpdateSupplierAsync(entity.Id, entity.Code, entity.Name, entity.Phone, entity.Email, entity.Note, entity.IsActive, entity.UpdatedAt, entity.Version, cancellationToken),
            async () => await masterAccessor.QuerySupplierAsync(entity.Id, cancellationToken) is { IsDeleted: false });
    }

    public async ValueTask<DataWriteStatus> DeleteAsync(Guid id, CancellationToken cancellationToken) =>
        await masterAccessor.DeleteSupplierAsync(id, timeProvider.GetUtcNow().UtcDateTime, cancellationToken) > 0 ? DataWriteStatus.Success : DataWriteStatus.NotFound;
}
