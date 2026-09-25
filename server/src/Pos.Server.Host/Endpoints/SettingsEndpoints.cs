namespace Pos.Server.Host.Endpoints;

using Pos.Contract.Settings;
using Pos.Server.Models.Entity;
using Pos.Server.Services;

using Smart.Mapper;

public static partial class SettingsEndpoints
{
    //--------------------------------------------------------------------------------
    // Mapping
    //--------------------------------------------------------------------------------

    public static void MapSettingsEndpoints(this WebApplication app)
    {
        var group = app.MapApiGroup(ApiRoutes.Settings);
        group.MapGet("/", HandleGetAsync);
        group.MapPut("/", HandleUpdateAsync).RequireAuthorization(Policies.Administrator);
    }

    //--------------------------------------------------------------------------------
    // Mapper
    //--------------------------------------------------------------------------------

    [Mapper]
    internal static partial SettingsResponse ToResponse(SettingsEntity entity);

    [Mapper]
    private static partial SettingsEntity ToEntity(SettingsUpdateRequest request);

    //--------------------------------------------------------------------------------
    // Handler
    //--------------------------------------------------------------------------------

    private static async ValueTask<IResult> HandleGetAsync(
        SettingsService service,
        CancellationToken cancellationToken)
    {
        var entity = await service.QueryAsync(cancellationToken);
        return entity is null ? ApiProblems.NotFound() : TypedResults.Ok(ToResponse(entity));
    }

    private static async ValueTask<IResult> HandleUpdateAsync(
        SettingsService service,
        SettingsUpdateRequest request,
        CancellationToken cancellationToken)
    {
        var result = await service.UpdateAsync(ToEntity(request), cancellationToken);
        return result.Status == DataWriteStatus.Success
            ? TypedResults.Ok(ToResponse(result.Entity!))
            : ApiProblems.FromStatus(result.Status);
    }
}
