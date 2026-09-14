namespace Pos.Server.Host.Endpoints;

using Pos.Contract.Sync;
using Pos.Server.Services;

// 端末のマスタ同期: since 以降に更新されたマスタをまとめて返す
public static class SyncEndpoints
{
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
        SyncService service,
        DateTime? since,
        CancellationToken cancellationToken)
    {
        var data = await service.QueryMastersAsync(since, cancellationToken);
        return TypedResults.Ok(new SyncMastersResponse
        {
            ServerTime = data.ServerTime,
            Settings = data.Settings is null ? null : SettingsEndpoints.ToResponse(data.Settings),
            Stores = data.Stores.Select(StoreEndpoints.ToResponse).ToList(),
            Terminals = data.Terminals.Select(TerminalEndpoints.ToResponse).ToList(),
            Staff = data.Staff.Select(StaffEndpoints.ToResponse).ToList(),
            Categories = data.Categories.Select(CategoryEndpoints.ToResponse).ToList(),
            TaxRates = data.TaxRates.Select(TaxRateEndpoints.ToResponse).ToList(),
            Products = data.Products.Select(ProductEndpoints.ToResponse).ToList(),
            Discounts = data.Discounts.Select(DiscountEndpoints.ToResponse).ToList(),
            PaymentMethods = data.PaymentMethods.Select(PaymentMethodEndpoints.ToResponse).ToList(),
            AdjustmentReasons = data.AdjustmentReasons.Select(InventoryEndpoints.ToResponse).ToList(),
            ProductsTruncated = false
        });
    }
}
