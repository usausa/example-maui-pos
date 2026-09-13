namespace Pos.Server.Host.Endpoints;

using Pos.Server.Accessors;
using Pos.Server.Host.Application;
using Pos.Server.Host.Mappers;
using Pos.Shared.Sync;

// 端末のマスタ同期 (api-design §3.10): since 以降に更新されたマスタをまとめて返す
public static class SyncEndpoints
{
    private const string SyncSort = "UpdatedAt, Id";

    //--------------------------------------------------------------------------------
    // Mapping
    //--------------------------------------------------------------------------------

    public static void MapSyncEndpoints(this WebApplication app)
    {
        var group = app.MapGroup(ApiRoutes.Sync);

        group.MapGet("/masters", HandleMastersAsync);
    }

    //--------------------------------------------------------------------------------
    // Handler
    //--------------------------------------------------------------------------------

    private static async ValueTask<IResult> HandleMastersAsync(
        SettingsAccessor settingsAccessor,
        StoreAccessor storeAccessor,
        TerminalAccessor terminalAccessor,
        StaffAccessor staffAccessor,
        CategoryAccessor categoryAccessor,
        TaxRateAccessor taxRateAccessor,
        ProductAccessor productAccessor,
        DiscountAccessor discountAccessor,
        PaymentMethodAccessor paymentMethodAccessor,
        AdjustmentReasonAccessor adjustmentReasonAccessor,
        TimeProvider timeProvider,
        DateTime? since,
        CancellationToken cancellationToken)
    {
        var serverTime = timeProvider.GetUtcNow().UtcDateTime;

        var settings = await settingsAccessor.QueryAsync(cancellationToken);
        var stores = await storeAccessor.QueryListAsync(since, true, SyncSort, Int32.MaxValue, 0, cancellationToken);
        var terminals = await terminalAccessor.QueryListAsync(null, since, true, SyncSort, Int32.MaxValue, 0, cancellationToken);
        var staff = await staffAccessor.QueryListAsync(null, since, true, SyncSort, Int32.MaxValue, 0, cancellationToken);
        var categories = await categoryAccessor.QueryListAsync(since, true, SyncSort, Int32.MaxValue, 0, cancellationToken);
        var taxRates = await taxRateAccessor.QueryListAsync(since, true, cancellationToken);
        var products = await productAccessor.QueryListAsync(null, null, null, since, true, SyncSort, Int32.MaxValue, 0, cancellationToken);
        var discounts = await discountAccessor.QueryListAsync(since, true, cancellationToken);
        var paymentMethods = await paymentMethodAccessor.QueryListAsync(since, true, cancellationToken);
        var adjustmentReasons = await adjustmentReasonAccessor.QueryListAsync(since, true, cancellationToken);

        return TypedResults.Ok(new SyncMastersResponse
        {
            ServerTime = serverTime,
            Settings = (settings is not null) && ((since is null) || (settings.UpdatedAt > since.Value)) ? MasterMapper.ToSettingsResponse(settings) : null,
            Stores = stores.Select(MasterMapper.ToStoreResponse).ToList(),
            Terminals = terminals.Select(MasterMapper.ToTerminalResponse).ToList(),
            Staff = staff.Select(MasterMapper.ToStaffResponse).ToList(),
            Categories = categories.Select(MasterMapper.ToCategoryResponse).ToList(),
            TaxRates = taxRates.Select(MasterMapper.ToTaxRateResponse).ToList(),
            Products = products.Select(MasterMapper.ToProductResponse).ToList(),
            Discounts = discounts.Select(MasterMapper.ToDiscountResponse).ToList(),
            PaymentMethods = paymentMethods.Select(MasterMapper.ToPaymentMethodResponse).ToList(),
            AdjustmentReasons = adjustmentReasons.Select(MasterMapper.ToAdjustmentReasonResponse).ToList(),
            ProductsTruncated = false
        });
    }
}
