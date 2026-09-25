namespace Pos.Server.Services;

using Pos.Domain.Logic;
using Pos.Server.Accessors;
using Pos.Server.Models;
using Pos.Server.Models.Entity;

public sealed class StaffService
{
    private readonly TimeProvider timeProvider;
    private readonly IDialect dialect;
    private readonly MasterAccessor masterAccessor;

    public StaffService(
        TimeProvider timeProvider,
        IDialect dialect,
        MasterAccessor masterAccessor)
    {
        this.timeProvider = timeProvider;
        this.dialect = dialect;
        this.masterAccessor = masterAccessor;
    }

    // storeId 指定時は本部 (StoreId = NULL) も含める
    public async ValueTask<PagedResult<StaffEntity>> QueryPageAsync(Guid? storeId, DateTime? updatedSince, bool includeDeleted, StaffSort sort, bool desc, int page, int size, CancellationToken cancellationToken)
    {
        var total = await masterAccessor.CountStaffAsync(storeId, updatedSince, includeDeleted, cancellationToken);
        var items = await masterAccessor.QueryStaffListAsync(storeId, updatedSince, includeDeleted, sort, desc, size, page * size, cancellationToken);
        return new PagedResult<StaffEntity>((int)total, page, size, items);
    }

    // 全件 (コード順)
    public ValueTask<List<StaffEntity>> QueryAllAsync(bool includeDeleted, CancellationToken cancellationToken) =>
        masterAccessor.QueryStaffAllAsync(includeDeleted, cancellationToken);

    public ValueTask<StaffEntity?> QueryAsync(Guid id, CancellationToken cancellationToken) =>
        masterAccessor.QueryStaffAsync(id, cancellationToken);

    public ValueTask<DataWriteStatus> InsertAsync(StaffEntity entity, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;
        entity.Id = Guid.CreateVersion7();
        entity.CreatedAt = now;
        entity.UpdatedAt = now;
        entity.Version = 1;
        return ServiceHelper.InsertAsync(dialect, () => masterAccessor.InsertStaffAsync(entity, cancellationToken));
    }

    public ValueTask<DataWriteResult<StaffEntity>> UpdateAsync(StaffEntity entity, CancellationToken cancellationToken)
    {
        entity.UpdatedAt = timeProvider.GetUtcNow().UtcDateTime;
        return ServiceHelper.UpdateAsync(
            dialect,
            () => masterAccessor.UpdateStaffAsync(entity.Id, entity.Code, entity.Name, entity.Role, entity.StoreId, entity.IsActive, entity.UpdatedAt, entity.Version, cancellationToken),
            async () => await masterAccessor.QueryStaffAsync(entity.Id, cancellationToken) is { IsDeleted: false });
    }

    // PIN の設定。形式 (4〜6 桁の数字) は呼び出し側が PinHasher.IsValidFormat で検証する
    public ValueTask<DataWriteResult<StaffEntity>> UpdatePinAsync(Guid id, string pin, int version, CancellationToken cancellationToken) =>
        ServiceHelper.UpdateAsync(
            dialect,
            () => masterAccessor.UpdateStaffPinAsync(id, PinHasher.Hash(pin), timeProvider.GetUtcNow().UtcDateTime, version, cancellationToken),
            async () => await masterAccessor.QueryStaffAsync(id, cancellationToken) is { IsDeleted: false });

    public async ValueTask<DataWriteStatus> DeleteAsync(Guid id, CancellationToken cancellationToken) =>
        await masterAccessor.DeleteStaffAsync(id, timeProvider.GetUtcNow().UtcDateTime, cancellationToken) > 0 ? DataWriteStatus.Success : DataWriteStatus.NotFound;
}
