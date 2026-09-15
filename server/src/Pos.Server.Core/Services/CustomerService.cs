namespace Pos.Server.Services;

using Pos.Server.Accessors;
using Pos.Server.Models;
using Pos.Server.Models.Entity;
using Pos.Server.Models.Parameters;

// 会員とポイント (残高は履歴の集計を非正規化したもの。加減算と履歴は同じトランザクションで書く)
public sealed class CustomerService
{
    private readonly IDbProvider provider;
    private readonly IDialect dialect;
    private readonly CustomerAccessor customerAccessor;
    private readonly TimeProvider timeProvider;

    public CustomerService(
        IDbProvider provider,
        IDialect dialect,
        CustomerAccessor customerAccessor,
        TimeProvider timeProvider)
    {
        this.provider = provider;
        this.dialect = dialect;
        this.customerAccessor = customerAccessor;
        this.timeProvider = timeProvider;
    }

    //--------------------------------------------------------------------------------
    // Customer
    //--------------------------------------------------------------------------------

    public async ValueTask<PagedResult<CustomerEntity>> QueryPageAsync(CustomerQueryParameter parameter, CancellationToken cancellationToken)
    {
        var keyword = ServiceHelper.ToLikePattern(dialect, parameter.Keyword);
        var total = await customerAccessor.CountAsync(keyword, parameter.Code, parameter.Phone, parameter.UpdatedSince, parameter.IncludeDeleted, cancellationToken);
        var items = await customerAccessor.QueryListAsync(keyword, parameter.Code, parameter.Phone, parameter.UpdatedSince, parameter.IncludeDeleted, parameter.Sort, parameter.Desc, parameter.Size, parameter.Page * parameter.Size, cancellationToken);
        return new PagedResult<CustomerEntity>((int)total, parameter.Page, parameter.Size, items);
    }

    public ValueTask<CustomerEntity?> QueryAsync(Guid id, CancellationToken cancellationToken) =>
        customerAccessor.QueryAsync(id, cancellationToken);

    public ValueTask<CustomerEntity?> QueryByCodeAsync(string code, CancellationToken cancellationToken) =>
        customerAccessor.QueryByCodeAsync(code, cancellationToken);

    // 残高がマイナスの会員 (要確認)
    public ValueTask<List<CustomerEntity>> QueryNegativePointListAsync(int limit, CancellationToken cancellationToken) =>
        customerAccessor.QueryNegativePointListAsync(limit, cancellationToken);

    public ValueTask<DataWriteStatus> InsertAsync(CustomerEntity entity, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;
        entity.Id = Guid.CreateVersion7();
        entity.PointBalance = 0;
        entity.CreatedAt = now;
        entity.UpdatedAt = now;
        entity.Version = 1;
        return ServiceHelper.InsertAsync(dialect, () => customerAccessor.InsertAsync(entity, cancellationToken));
    }

    // PointBalance は更新しない
    public ValueTask<DataWriteResult<CustomerEntity>> UpdateAsync(CustomerEntity entity, CancellationToken cancellationToken)
    {
        entity.UpdatedAt = timeProvider.GetUtcNow().UtcDateTime;
        return ServiceHelper.UpdateAsync(
            dialect,
            () => customerAccessor.UpdateAsync(
                entity.Id,
                entity.Code,
                entity.Name,
                entity.Kana,
                entity.Phone,
                entity.Email,
                entity.PostalCode,
                entity.Address,
                entity.BirthDate,
                entity.Note,
                entity.UpdatedAt,
                entity.Version,
                cancellationToken),
            async () => await customerAccessor.QueryAsync(entity.Id, cancellationToken) is { IsDeleted: false });
    }

    public async ValueTask<DataWriteStatus> DeleteAsync(Guid id, CancellationToken cancellationToken) =>
        await customerAccessor.DeleteAsync(id, timeProvider.GetUtcNow().UtcDateTime, cancellationToken) > 0 ? DataWriteStatus.Success : DataWriteStatus.NotFound;

    //--------------------------------------------------------------------------------
    // Points
    //--------------------------------------------------------------------------------

    // 会員がなければ null
    public async ValueTask<PagedResult<PointHistoryEntity>?> QueryPointHistoryPageAsync(Guid customerId, int page, int size, CancellationToken cancellationToken)
    {
        if (await customerAccessor.QueryAsync(customerId, cancellationToken) is null)
        {
            return null;
        }

        var total = await customerAccessor.CountPointHistoryAsync(customerId, cancellationToken);
        var items = await customerAccessor.QueryPointHistoryListAsync(customerId, size, page * size, cancellationToken);
        return new PagedResult<PointHistoryEntity>((int)total, page, size, items);
    }

    // 手動調整 (Adjust 履歴を作り、残高を加減算する)。会員がなければ null
    public async ValueTask<PointHistoryEntity?> AdjustPointsAsync(Guid customerId, int points, string? reason, Guid? staffId, CancellationToken cancellationToken)
    {
        if (await customerAccessor.QueryAsync(customerId, cancellationToken) is null)
        {
            return null;
        }

        var now = timeProvider.GetUtcNow().UtcDateTime;
        return await provider.UsingTxAsync(async (_, tx) =>
        {
            var balance = await customerAccessor.AddPointsAsync(tx, customerId, points, now, cancellationToken);
            var entity = new PointHistoryEntity
            {
                Id = Guid.CreateVersion7(),
                CustomerId = customerId,
                Type = PointHistoryType.Adjust,
                Points = points,
                BalanceAfter = balance,
                Reason = reason,
                StaffId = staffId,
                OccurredAt = now,
                CreatedAt = now
            };
            await customerAccessor.InsertPointHistoryAsync(tx, entity, cancellationToken);
            await tx.CommitAsync(cancellationToken);
            return entity;
        }, cancellationToken);
    }
}
