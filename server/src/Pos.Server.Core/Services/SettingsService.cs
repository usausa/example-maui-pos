namespace Pos.Server.Services;

using Pos.Server.Accessors;
using Pos.Server.Models.Entity;

// 会社設定 (1 行)
public sealed class SettingsService
{
    private readonly MasterAccessor masterAccessor;
    private readonly TimeProvider timeProvider;

    public SettingsService(
        MasterAccessor masterAccessor,
        TimeProvider timeProvider)
    {
        this.masterAccessor = masterAccessor;
        this.timeProvider = timeProvider;
    }

    public ValueTask<SettingsEntity?> QueryAsync(CancellationToken cancellationToken) =>
        masterAccessor.QuerySettingsAsync(cancellationToken);

    // 更新 0 件は、行がなければ NotFound、あれば VersionMismatch
    public async ValueTask<DataWriteStatus> UpdateAsync(SettingsEntity entity, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(entity);

        entity.UpdatedAt = timeProvider.GetUtcNow().UtcDateTime;
        if (await masterAccessor.UpdateSettingsAsync(entity.CompanyName, entity.Currency, entity.TaxRounding, entity.PointBasis, entity.BusinessDayStartTime, entity.UpdatedAt, entity.Version, cancellationToken) > 0)
        {
            return DataWriteStatus.Success;
        }

        return await masterAccessor.QuerySettingsAsync(cancellationToken) is null ? DataWriteStatus.NotFound : DataWriteStatus.VersionMismatch;
    }
}
