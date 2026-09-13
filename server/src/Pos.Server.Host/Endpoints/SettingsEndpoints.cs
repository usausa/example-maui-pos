namespace Pos.Server.Host.Endpoints;

using Pos.Server.Accessors;
using Pos.Server.Host.Application;
using Pos.Server.Host.Infrastructure.Api;
using Pos.Server.Host.Mappers;
using Pos.Shared.Settings;

public static class SettingsEndpoints
{
    //--------------------------------------------------------------------------------
    // Mapping
    //--------------------------------------------------------------------------------

    public static void MapSettingsEndpoints(this WebApplication app)
    {
        var group = app.MapGroup(ApiRoutes.Settings);

        group.MapGet("/", HandleGetAsync);
        group.MapPut("/", HandleUpdateAsync);
    }

    //--------------------------------------------------------------------------------
    // Handler
    //--------------------------------------------------------------------------------

    private static async ValueTask<IResult> HandleGetAsync(
        SettingsAccessor accessor,
        CancellationToken cancellationToken)
    {
        var entity = await accessor.QueryAsync(cancellationToken);
        return entity is null ? ApiProblems.NotFound() : TypedResults.Ok(MasterMapper.ToSettingsResponse(entity));
    }

    private static async ValueTask<IResult> HandleUpdateAsync(
        SettingsAccessor accessor,
        TimeProvider timeProvider,
        SettingsUpdateRequest request,
        CancellationToken cancellationToken)
    {
        var rows = await accessor.UpdateAsync(request.CompanyName, request.Currency, request.TaxRounding, request.PointBasis, request.BusinessDayStartTime, timeProvider.GetUtcNow().UtcDateTime, request.Version, cancellationToken);
        var entity = await accessor.QueryAsync(cancellationToken);
        if (entity is null)
        {
            return ApiProblems.NotFound();
        }

        return rows == 0 ? ApiProblems.VersionMismatch() : TypedResults.Ok(MasterMapper.ToSettingsResponse(entity));
    }
}
