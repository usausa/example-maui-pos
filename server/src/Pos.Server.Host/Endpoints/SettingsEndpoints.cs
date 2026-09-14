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
        var group = app.MapGroup(ApiRoutes.Settings);
        group.MapGet("/", HandleGetAsync);
        group.MapPut("/", HandleUpdateAsync);
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
        var status = await service.UpdateAsync(ToEntity(request), cancellationToken);
        return status == DataWriteStatus.Success
            ? TypedResults.Ok(ToResponse((await service.QueryAsync(cancellationToken))!))
            : ApiProblems.FromStatus(status);
    }
}
