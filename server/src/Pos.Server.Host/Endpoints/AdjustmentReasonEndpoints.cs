namespace Pos.Server.Host.Endpoints;

using Pos.Contract.AdjustmentReasons;
using Pos.Server.Models.Entity;
using Pos.Server.Services;

using Smart.Mapper;

// 在庫調整理由 (少数なのでページングなし)
public static partial class AdjustmentReasonEndpoints
{
    //--------------------------------------------------------------------------------
    // Mapping
    //--------------------------------------------------------------------------------

    public static void MapAdjustmentReasonEndpoints(this WebApplication app)
    {
        var group = app.MapApiGroup(ApiRoutes.AdjustmentReasons);
        group.MapGet("/", HandleListAsync)
            .WithName("AdjustmentReasonList")
            .Produces<AdjustmentReasonListResponse>();
        group.MapGet("/{id:guid}", HandleGetAsync)
            .WithName("AdjustmentReasonGet")
            .Produces<AdjustmentReasonListResponseItem>()
            .ProducesProblem(StatusCodes.Status404NotFound);
        group.MapPost("/", HandleCreateAsync)
            .RequireAuthorization(Policies.Administrator)
            .WithName("AdjustmentReasonCreate")
            .Produces<AdjustmentReasonListResponseItem>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .Produces(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status409Conflict);
        group.MapPut("/{id:guid}", HandleUpdateAsync)
            .RequireAuthorization(Policies.Administrator)
            .WithName("AdjustmentReasonUpdate")
            .Produces<AdjustmentReasonListResponseItem>()
            .ProducesValidationProblem()
            .Produces(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);
        group.MapDelete("/{id:guid}", HandleDeleteAsync)
            .RequireAuthorization(Policies.Administrator)
            .WithName("AdjustmentReasonDelete")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);
    }

    //--------------------------------------------------------------------------------
    // Mapper
    //--------------------------------------------------------------------------------

    [Mapper]
    internal static partial AdjustmentReasonListResponseItem ToResponse(AdjustmentReasonEntity entity);

    [Mapper]
    private static partial AdjustmentReasonEntity ToEntity(AdjustmentReasonCreateRequest request);

    [Mapper]
    private static partial AdjustmentReasonEntity ToEntity(AdjustmentReasonUpdateRequest request);

    //--------------------------------------------------------------------------------
    // Handler
    //--------------------------------------------------------------------------------

    private static async ValueTask<IResult> HandleListAsync(
        AdjustmentReasonService service,
        DateTime? updatedSince,
        CancellationToken cancellationToken,
        bool includeDeleted = false)
    {
        var items = await service.QueryListAsync(updatedSince, includeDeleted, cancellationToken);
        return TypedResults.Ok(new AdjustmentReasonListResponse { Total = items.Count, Page = 0, Size = items.Count, Items = items.Select(ToResponse).ToList() });
    }

    private static async ValueTask<IResult> HandleGetAsync(
        AdjustmentReasonService service,
        Guid id,
        CancellationToken cancellationToken)
    {
        var entity = await service.QueryAsync(id, cancellationToken);
        return entity is null ? ApiProblems.NotFound() : TypedResults.Ok(ToResponse(entity));
    }

    private static async ValueTask<IResult> HandleCreateAsync(
        AdjustmentReasonService service,
        AdjustmentReasonCreateRequest request,
        CancellationToken cancellationToken)
    {
        var entity = ToEntity(request);
        var status = await service.InsertAsync(entity, cancellationToken);
        return status == DataWriteStatus.Success
            ? TypedResults.Created($"{ApiRoutes.AdjustmentReasons}/{entity.Id}", ToResponse(entity))
            : ApiProblems.DuplicateCode();
    }

    private static async ValueTask<IResult> HandleUpdateAsync(
        AdjustmentReasonService service,
        Guid id,
        AdjustmentReasonUpdateRequest request,
        CancellationToken cancellationToken)
    {
        var entity = ToEntity(request);
        entity.Id = id;
        var result = await service.UpdateAsync(entity, cancellationToken);
        return result.Status == DataWriteStatus.Success
            ? TypedResults.Ok(ToResponse(result.Entity!))
            : ApiProblems.FromStatus(result.Status);
    }

    private static async ValueTask<IResult> HandleDeleteAsync(
        AdjustmentReasonService service,
        Guid id,
        CancellationToken cancellationToken)
    {
        var status = await service.DeleteAsync(id, cancellationToken);
        return status == DataWriteStatus.Success ? TypedResults.NoContent() : ApiProblems.FromStatus(status);
    }
}
