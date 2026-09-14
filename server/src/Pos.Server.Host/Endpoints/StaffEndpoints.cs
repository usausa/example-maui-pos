namespace Pos.Server.Host.Endpoints;

using Pos.Contract.Staff;
using Pos.Server.Models.Entity;
using Pos.Server.Services;

using Smart.Mapper;

public static partial class StaffEndpoints
{
    //--------------------------------------------------------------------------------
    // Mapping
    //--------------------------------------------------------------------------------

    public static void MapStaffEndpoints(this WebApplication app)
    {
        var group = app.MapGroup(ApiRoutes.Staff);
        group.MapGet("/", HandleListAsync);
        group.MapGet("/{id:guid}", HandleGetAsync);
        group.MapPost("/", HandleCreateAsync);
        group.MapPut("/{id:guid}", HandleUpdateAsync);
        group.MapDelete("/{id:guid}", HandleDeleteAsync);
    }

    //--------------------------------------------------------------------------------
    // Mapper
    //--------------------------------------------------------------------------------

    [Mapper]
    internal static partial StaffResponseItem ToResponse(StaffEntity entity);

    [Mapper]
    private static partial StaffEntity ToEntity(StaffCreateRequest request);

    [Mapper]
    private static partial StaffEntity ToEntity(StaffUpdateRequest request);

    //--------------------------------------------------------------------------------
    // Handler
    //--------------------------------------------------------------------------------

    private static async ValueTask<IResult> HandleListAsync(
        StaffService service,
        Guid? storeId,
        DateTime? updatedSince,
        string? sort,
        CancellationToken cancellationToken,
        bool includeDeleted = false,
        bool desc = false,
        [Range(0, Int32.MaxValue)] int page = 0,
        [Range(1, ApiDefaults.MaxPageSize)] int size = ApiDefaults.PageSize)
    {
        var result = await service.QueryPageAsync(storeId, updatedSince, includeDeleted, sort, desc, page, size, cancellationToken);
        return TypedResults.Ok(new StaffResponse { Total = result.Total, Page = result.Page, Size = result.Size, Items = result.Items.Select(ToResponse).ToList() });
    }

    private static async ValueTask<IResult> HandleGetAsync(
        StaffService service,
        Guid id,
        CancellationToken cancellationToken)
    {
        var entity = await service.QueryAsync(id, cancellationToken);
        return entity is null ? ApiProblems.NotFound() : TypedResults.Ok(ToResponse(entity));
    }

    private static async ValueTask<IResult> HandleCreateAsync(
        StaffService service,
        StaffCreateRequest request,
        CancellationToken cancellationToken)
    {
        var entity = ToEntity(request);
        var status = await service.InsertAsync(entity, cancellationToken);
        return status == DataWriteStatus.Success
            ? TypedResults.Created($"{ApiRoutes.Staff}/{entity.Id}", ToResponse(entity))
            : ApiProblems.DuplicateCode();
    }

    private static async ValueTask<IResult> HandleUpdateAsync(
        StaffService service,
        Guid id,
        StaffUpdateRequest request,
        CancellationToken cancellationToken)
    {
        var entity = ToEntity(request);
        entity.Id = id;
        var status = await service.UpdateAsync(entity, cancellationToken);
        return status == DataWriteStatus.Success
            ? TypedResults.Ok(ToResponse((await service.QueryAsync(id, cancellationToken))!))
            : ApiProblems.FromStatus(status);
    }

    private static async ValueTask<IResult> HandleDeleteAsync(
        StaffService service,
        Guid id,
        CancellationToken cancellationToken)
    {
        var status = await service.DeleteAsync(id, cancellationToken);
        return status == DataWriteStatus.Success ? TypedResults.NoContent() : ApiProblems.FromStatus(status);
    }
}
