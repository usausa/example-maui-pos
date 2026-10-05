namespace Pos.Server.Host.Endpoints;

using Pos.Contract.Staff;
using Pos.Server.Host.Helpers;
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
        var group = app.MapApiGroup(ApiRoutes.Staff);
        group.MapGet("/", HandleListAsync)
            .WithName("StaffList")
            .Produces<StaffListResponse>()
            .ProducesValidationProblem();
        group.MapGet("/{id:guid}", HandleGetAsync)
            .WithName("StaffGet")
            .Produces<StaffListResponseItem>()
            .ProducesProblem(StatusCodes.Status404NotFound);
        group.MapPost("/", HandleCreateAsync)
            .RequireAuthorization(Policies.Administrator)
            .WithName("StaffCreate")
            .Produces<StaffListResponseItem>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .Produces(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status409Conflict);
        group.MapPut("/{id:guid}", HandleUpdateAsync)
            .RequireAuthorization(Policies.Administrator)
            .WithName("StaffUpdate")
            .Produces<StaffListResponseItem>()
            .ProducesValidationProblem()
            .Produces(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);
        group.MapDelete("/{id:guid}", HandleDeleteAsync)
            .RequireAuthorization(Policies.Administrator)
            .WithName("StaffDelete")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);
    }

    //--------------------------------------------------------------------------------
    // Mapper
    //--------------------------------------------------------------------------------

    // PIN のハッシュは端末向けの同期応答 (ToSyncResponse) にだけ含める
    [Mapper]
    [MapIgnore(nameof(StaffListResponseItem.PinHash))]
    internal static partial StaffListResponseItem ToListResponseItem(StaffEntity entity);

    internal static StaffListResponseItem ToSyncResponse(StaffEntity entity)
    {
        var response = ToListResponseItem(entity);
        response.PinHash = entity.PinHash;
        return response;
    }

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
        var result = await service.QueryPageAsync(storeId, updatedSince, includeDeleted, EnumHelper.Parse(sort, StaffSort.Code), desc, page, size, cancellationToken);
        return TypedResults.Ok(new StaffListResponse { Total = result.Total, Page = result.Page, Size = result.Size, Items = result.Items.Select(ToListResponseItem).ToList() });
    }

    private static async ValueTask<IResult> HandleGetAsync(
        StaffService service,
        Guid id,
        CancellationToken cancellationToken)
    {
        var entity = await service.QueryAsync(id, cancellationToken);
        return entity is null ? ApiProblems.NotFound() : TypedResults.Ok(ToListResponseItem(entity));
    }

    private static async ValueTask<IResult> HandleCreateAsync(
        StaffService service,
        StaffCreateRequest request,
        CancellationToken cancellationToken)
    {
        var entity = ToEntity(request);
        var status = await service.InsertAsync(entity, cancellationToken);
        return status == DataWriteStatus.Success
            ? TypedResults.Created($"{ApiRoutes.Staff}/{entity.Id}", ToListResponseItem(entity))
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
        var result = await service.UpdateAsync(entity, cancellationToken);
        return result.Status == DataWriteStatus.Success
            ? TypedResults.Ok(ToListResponseItem(result.Entity!))
            : ApiProblems.FromStatus(result.Status);
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
