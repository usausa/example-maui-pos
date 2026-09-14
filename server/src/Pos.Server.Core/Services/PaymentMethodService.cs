namespace Pos.Server.Services;

using Pos.Server.Accessors;
using Pos.Server.Models.Entity;

// 支払方法 (少数なのでページングなし)。Kind = Points かつ有効な行はちょうど 1 件
public sealed class PaymentMethodService
{
    private readonly IDialect dialect;
    private readonly MasterAccessor masterAccessor;
    private readonly TimeProvider timeProvider;

    public PaymentMethodService(
        IDialect dialect,
        MasterAccessor masterAccessor,
        TimeProvider timeProvider)
    {
        this.dialect = dialect;
        this.masterAccessor = masterAccessor;
        this.timeProvider = timeProvider;
    }

    public ValueTask<List<PaymentMethodEntity>> QueryListAsync(DateTime? updatedSince, bool includeDeleted, CancellationToken cancellationToken) =>
        masterAccessor.QueryPaymentMethodListAsync(updatedSince, includeDeleted, cancellationToken);

    public ValueTask<PaymentMethodEntity?> QueryAsync(Guid id, CancellationToken cancellationToken) =>
        masterAccessor.QueryPaymentMethodAsync(id, cancellationToken);

    // 2 件目の有効なポイント支払は Invalid
    public async ValueTask<DataWriteStatus> InsertAsync(PaymentMethodEntity entity, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(entity);

        var now = timeProvider.GetUtcNow().UtcDateTime;
        entity.Id = Guid.CreateVersion7();
        entity.CreatedAt = now;
        entity.UpdatedAt = now;
        entity.Version = 1;
        if (await IsSecondPointsMethodAsync(entity, cancellationToken))
        {
            return DataWriteStatus.Invalid;
        }

        return await ServiceHelper.InsertAsync(dialect, () => masterAccessor.InsertPaymentMethodAsync(entity, cancellationToken));
    }

    public async ValueTask<DataWriteStatus> UpdateAsync(PaymentMethodEntity entity, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(entity);

        if (await IsSecondPointsMethodAsync(entity, cancellationToken))
        {
            return DataWriteStatus.Invalid;
        }

        entity.UpdatedAt = timeProvider.GetUtcNow().UtcDateTime;
        return await ServiceHelper.UpdateAsync(
            dialect,
            () => masterAccessor.UpdatePaymentMethodAsync(
                entity.Id,
                entity.Code,
                entity.Name,
                entity.ShortName,
                entity.Kind,
                entity.AllowsChange,
                entity.RequiresReference,
                entity.IsActive,
                entity.SortOrder,
                entity.UpdatedAt,
                entity.Version,
                cancellationToken),
            async () => await masterAccessor.QueryPaymentMethodAsync(entity.Id, cancellationToken) is { IsDeleted: false });
    }

    public async ValueTask<DataWriteStatus> DeleteAsync(Guid id, CancellationToken cancellationToken) =>
        await masterAccessor.DeletePaymentMethodAsync(id, timeProvider.GetUtcNow().UtcDateTime, cancellationToken) > 0 ? DataWriteStatus.Success : DataWriteStatus.NotFound;

    private async ValueTask<bool> IsSecondPointsMethodAsync(PaymentMethodEntity entity, CancellationToken cancellationToken) =>
        (entity.Kind == PaymentKind.Points) && entity.IsActive && (await masterAccessor.CountActivePointsPaymentMethodsAsync(entity.Id, cancellationToken) > 0);
}
