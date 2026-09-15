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

    // 更新後の行が返らなければ、行がなければ NotFound、あれば VersionMismatch
    public async ValueTask<DataWriteResult<SettingsEntity>> UpdateAsync(SettingsEntity entity, CancellationToken cancellationToken)
    {
        entity.UpdatedAt = timeProvider.GetUtcNow().UtcDateTime;
        var updated = await masterAccessor.UpdateSettingsAsync(entity.CompanyName, entity.Currency, entity.TaxRounding, entity.PointBasis, entity.BusinessDayStartTime, entity.UpdatedAt, entity.Version, cancellationToken);
        if (updated is not null)
        {
            return new DataWriteResult<SettingsEntity>(DataWriteStatus.Success, updated);
        }

        return new DataWriteResult<SettingsEntity>(await masterAccessor.QuerySettingsAsync(cancellationToken) is null ? DataWriteStatus.NotFound : DataWriteStatus.VersionMismatch, null);
    }
}
