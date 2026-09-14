namespace Pos.Server.Services;

using Pos.Server.Accessors;
using Pos.Server.Models.Entity;

// 税率 (少数なのでページングなし)
public sealed class TaxRateService
{
    private readonly IDialect dialect;
    private readonly MasterAccessor masterAccessor;
    private readonly TimeProvider timeProvider;

    public TaxRateService(
        IDialect dialect,
        MasterAccessor masterAccessor,
        TimeProvider timeProvider)
    {
        this.dialect = dialect;
        this.masterAccessor = masterAccessor;
        this.timeProvider = timeProvider;
    }

    public ValueTask<List<TaxRateEntity>> QueryListAsync(DateTime? updatedSince, bool includeDeleted, CancellationToken cancellationToken) =>
        masterAccessor.QueryTaxRateListAsync(updatedSince, includeDeleted, cancellationToken);

    public ValueTask<TaxRateEntity?> QueryAsync(Guid id, CancellationToken cancellationToken) =>
        masterAccessor.QueryTaxRateAsync(id, cancellationToken);

    // 既定は 1 件だけ (IsDefault なら他を落とす)
    public async ValueTask<DataWriteStatus> InsertAsync(TaxRateEntity entity, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(entity);

        var now = timeProvider.GetUtcNow().UtcDateTime;
        entity.Id = Guid.CreateVersion7();
        entity.CreatedAt = now;
        entity.UpdatedAt = now;
        entity.Version = 1;
        var status = await ServiceHelper.InsertAsync(dialect, () => masterAccessor.InsertTaxRateAsync(entity, cancellationToken));
        if ((status == DataWriteStatus.Success) && entity.IsDefault)
        {
            await masterAccessor.ClearDefaultTaxRateAsync(entity.Id, now, cancellationToken);
        }

        return status;
    }

    public async ValueTask<DataWriteStatus> UpdateAsync(TaxRateEntity entity, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(entity);

        entity.UpdatedAt = timeProvider.GetUtcNow().UtcDateTime;
        var status = await ServiceHelper.UpdateAsync(
            dialect,
            () => masterAccessor.UpdateTaxRateAsync(entity.Id, entity.Code, entity.Name, entity.Rate, entity.Kind, entity.IsDefault, entity.SortOrder, entity.UpdatedAt, entity.Version, cancellationToken),
            async () => await masterAccessor.QueryTaxRateAsync(entity.Id, cancellationToken) is { IsDeleted: false });
        if ((status == DataWriteStatus.Success) && entity.IsDefault)
        {
            await masterAccessor.ClearDefaultTaxRateAsync(entity.Id, entity.UpdatedAt, cancellationToken);
        }

        return status;
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
