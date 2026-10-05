namespace Pos.Server.Host.Endpoints;

using Pos.Contract.Discounts;
using Pos.Server.Models.Entity;
using Pos.Server.Services;

using Smart.Mapper;

public static partial class DiscountEndpoints
{
    //--------------------------------------------------------------------------------
    // Mapping
    //--------------------------------------------------------------------------------

    public static void MapDiscountEndpoints(this WebApplication app)
    {
        var group = app.MapApiGroup(ApiRoutes.Discounts);
        group.MapGet("/", HandleListAsync)
            .WithName("DiscountList")
            .Produces<DiscountListResponse>();
        group.MapGet("/{id:guid}", HandleGetAsync)
            .WithName("DiscountGet")
            .Produces<DiscountListResponseItem>()
            .ProducesProblem(StatusCodes.Status404NotFound);
        group.MapPost("/", HandleCreateAsync)
            .RequireAuthorization(Policies.Administrator)
            .WithName("DiscountCreate")
            .Produces<DiscountListResponseItem>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .Produces(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status409Conflict);
        group.MapPut("/{id:guid}", HandleUpdateAsync)
            .RequireAuthorization(Policies.Administrator)
            .WithName("DiscountUpdate")
            .Produces<DiscountListResponseItem>()
            .ProducesValidationProblem()
            .Produces(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);
        group.MapDelete("/{id:guid}", HandleDeleteAsync)
            .RequireAuthorization(Policies.Administrator)
            .WithName("DiscountDelete")
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
    internal static partial DiscountListResponseItem ToListResponseItem(DiscountEntity entity);

    [Mapper]
    private static partial DiscountEntity ToEntity(DiscountCreateRequest request);

    [Mapper]
    private static partial DiscountEntity ToEntity(DiscountUpdateRequest request);

    //--------------------------------------------------------------------------------
    // Handler
    //--------------------------------------------------------------------------------

    // 少数なのでページングなし
    private static async ValueTask<IResult> HandleListAsync(
        DiscountService service,
        DateTime? updatedSince,
        CancellationToken cancellationToken,
        bool includeDeleted = false)
    {
        var items = await service.QueryListAsync(updatedSince, includeDeleted, cancellationToken);
        return TypedResults.Ok(new DiscountListResponse { Total = items.Count, Page = 0, Size = items.Count, Items = items.Select(ToListResponseItem).ToList() });
    }

    private static async ValueTask<IResult> HandleGetAsync(
        DiscountService service,
        Guid id,
        CancellationToken cancellationToken)
    {
        var entity = await service.QueryAsync(id, cancellationToken);
        return entity is null ? ApiProblems.NotFound() : TypedResults.Ok(ToListResponseItem(entity));
    }

    private static async ValueTask<IResult> HandleCreateAsync(
        DiscountService service,
        DiscountCreateRequest request,
        CancellationToken cancellationToken)
    {
        var entity = ToEntity(request);
        var status = await service.InsertAsync(entity, cancellationToken);
        return status == DataWriteStatus.Success
            ? TypedResults.Created($"{ApiRoutes.Discounts}/{entity.Id}", ToListResponseItem(entity))
            : ApiProblems.DuplicateCode();
    }

    private static async ValueTask<IResult> HandleUpdateAsync(
        DiscountService service,
        Guid id,
        DiscountUpdateRequest request,
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
        DiscountService service,
        Guid id,
        CancellationToken cancellationToken)
    {
        var status = await service.DeleteAsync(id, cancellationToken);
        return status == DataWriteStatus.Success ? TypedResults.NoContent() : ApiProblems.FromStatus(status);
    }
}
