namespace Pos.Server.Services;

using Pos.Server.Accessors;
using Pos.Server.Models.Entity;

// 税率 (少数なのでページングなし)
public sealed class TaxRateService
{
    private readonly TimeProvider timeProvider;
    private readonly IDbProvider provider;
    private readonly IDialect dialect;
    private readonly MasterAccessor masterAccessor;

    public TaxRateService(
        TimeProvider timeProvider,
        IDbProvider provider,
        IDialect dialect,
        MasterAccessor masterAccessor)
    {
        this.timeProvider = timeProvider;
        this.provider = provider;
        this.dialect = dialect;
        this.masterAccessor = masterAccessor;
    }

    public ValueTask<List<TaxRateEntity>> QueryListAsync(DateTime? updatedSince, bool includeDeleted, CancellationToken cancellationToken) =>
        masterAccessor.QueryTaxRateListAsync(updatedSince, includeDeleted, cancellationToken);

    public ValueTask<TaxRateEntity?> QueryAsync(Guid id, CancellationToken cancellationToken) =>
        masterAccessor.QueryTaxRateAsync(id, cancellationToken);

    // 既定は 1 件だけ (IsDefault なら同じトランザクションで他を落とす)
    public ValueTask<DataWriteStatus> InsertAsync(TaxRateEntity entity, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;
        entity.Id = Guid.CreateVersion7();
        entity.CreatedAt = now;
        entity.UpdatedAt = now;
        entity.Version = 1;
        return ServiceHelper.InsertAsync(dialect, () => provider.UsingTxAsync(async (_, tx) =>
        {
            var count = await masterAccessor.InsertTaxRateAsync(tx, entity, cancellationToken);
            if (entity.IsDefault)
            {
                await masterAccessor.UpdateTaxRateDefaultClearedAsync(tx, entity.Id, now, cancellationToken);
            }

            await tx.CommitAsync(cancellationToken);
            return count;
        }, cancellationToken));
    }

    // 版が合わないときは何も書かない。行があるかはトランザクションを閉じてから確かめる
    public ValueTask<DataWriteResult<TaxRateEntity>> UpdateAsync(TaxRateEntity entity, CancellationToken cancellationToken)
    {
        entity.UpdatedAt = timeProvider.GetUtcNow().UtcDateTime;
        return ServiceHelper.UpdateAsync(
            dialect,
            () => provider.UsingTxAsync(async (_, tx) =>
            {
                var updated = await masterAccessor.UpdateTaxRateAsync(tx, entity.Id, entity.Code, entity.Name, entity.Rate, entity.Kind, entity.IsDefault, entity.SortOrder, entity.UpdatedAt, entity.Version, cancellationToken);
                if (updated is null)
                {
                    return null;
                }

                if (updated.IsDefault)
                {
                    await masterAccessor.UpdateTaxRateDefaultClearedAsync(tx, updated.Id, updated.UpdatedAt, cancellationToken);
                }

                await tx.CommitAsync(cancellationToken);
                return updated;
            }, cancellationToken),
            async () => await masterAccessor.QueryTaxRateAsync(entity.Id, cancellationToken) is { IsDeleted: false });
    }

    // 使用中の商品がある税率は削除できない
    public async ValueTask<DataWriteStatus> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        if (await masterAccessor.CountTaxRateProductsAsync(id, cancellationToken) > 0)
        {
            return DataWriteStatus.InUse;
        }

        return await masterAccessor.DeleteTaxRateAsync(id, timeProvider.GetUtcNow().UtcDateTime, cancellationToken) > 0 ? DataWriteStatus.Success : DataWriteStatus.NotFound;
    }
}
