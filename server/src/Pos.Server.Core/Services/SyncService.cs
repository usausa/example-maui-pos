namespace Pos.Server.Services;

using Pos.Server.Accessors;
using Pos.Server.Models.Views;

// 端末のマスタ同期: since 以降に更新されたマスタをまとめて返す
public sealed class SyncService
{
    private readonly MasterAccessor masterAccessor;
    private readonly ProductAccessor productAccessor;
    private readonly TimeProvider timeProvider;

    public SyncService(
        MasterAccessor masterAccessor,
        ProductAccessor productAccessor,
        TimeProvider timeProvider)
    {
        this.masterAccessor = masterAccessor;
        this.productAccessor = productAccessor;
        this.timeProvider = timeProvider;
    }

    public async ValueTask<SyncMasterData> QueryMastersAsync(DateTime? since, CancellationToken cancellationToken)
    {
        var serverTime = timeProvider.GetUtcNow().UtcDateTime;
        var settings = await masterAccessor.QuerySettingsAsync(cancellationToken);
        return new SyncMasterData
        {
            ServerTime = serverTime,
            Settings = (settings is not null) && ((since is null) || (settings.UpdatedAt > since.Value)) ? settings : null,
            Stores = await masterAccessor.QueryStoreListAsync(since, true, SqlHelper.SyncSort, Int32.MaxValue, 0, cancellationToken),
            Terminals = await masterAccessor.QueryTerminalListAsync(null, since, true, SqlHelper.SyncSort, Int32.MaxValue, 0, cancellationToken),
            Staff = await masterAccessor.QueryStaffListAsync(null, since, true, SqlHelper.SyncSort, Int32.MaxValue, 0, cancellationToken),
            Categories = await masterAccessor.QueryCategoryListAsync(since, true, SqlHelper.SyncSort, Int32.MaxValue, 0, cancellationToken),
            TaxRates = await masterAccessor.QueryTaxRateListAsync(since, true, cancellationToken),
            Products = await productAccessor.QueryListAsync(null, null, null, since, true, SqlHelper.SyncSort, Int32.MaxValue, 0, cancellationToken),
            Discounts = await masterAccessor.QueryDiscountListAsync(since, true, cancellationToken),
            PaymentMethods = await masterAccessor.QueryPaymentMethodListAsync(since, true, cancellationToken),
            AdjustmentReasons = await masterAccessor.QueryAdjustmentReasonListAsync(since, true, cancellationToken)
        };
    }
}
