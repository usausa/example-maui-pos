namespace Pos.Server.Host.Endpoints;

using Pos.Contract.Suppliers;
using Pos.Server.Models.Entity;
using Pos.Server.Services;

using Smart.Mapper;

// 仕入先 (少数なのでページングなし)
public static partial class SupplierEndpoints
{
    //--------------------------------------------------------------------------------
    // Mapping
    //--------------------------------------------------------------------------------

    public static void MapSupplierEndpoints(this WebApplication app)
    {
        // 仕入先は管理画面だけで使う
        var group = app.MapApiGroup(ApiRoutes.Suppliers).RequireAuthorization(Policies.Admin);
        group.MapGet("/", HandleListAsync)
            .WithName("SupplierList")
            .Produces<SupplierListResponse>();
        group.MapGet("/{id:guid}", HandleGetAsync)
            .WithName("SupplierGet")
            .Produces<SupplierListResponseItem>()
            .ProducesProblem(StatusCodes.Status404NotFound);
        group.MapPost("/", HandleCreateAsync)
            .RequireAuthorization(Policies.Administrator)
            .WithName("SupplierCreate")
            .Produces<SupplierListResponseItem>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .Produces(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status409Conflict);
        group.MapPut("/{id:guid}", HandleUpdateAsync)
            .RequireAuthorization(Policies.Administrator)
            .WithName("SupplierUpdate")
            .Produces<SupplierListResponseItem>()
            .ProducesValidationProblem()
            .Produces(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);
        group.MapDelete("/{id:guid}", HandleDeleteAsync)
            .RequireAuthorization(Policies.Administrator)
            .WithName("SupplierDelete")
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
    private static partial SupplierListResponseItem ToListResponseItem(SupplierEntity entity);

    [Mapper]
    private static partial SupplierEntity ToEntity(SupplierCreateRequest request);

    [Mapper]
    private static partial SupplierEntity ToEntity(SupplierUpdateRequest request);

    //--------------------------------------------------------------------------------
    // Handler
    //--------------------------------------------------------------------------------

    private static async ValueTask<IResult> HandleListAsync(
        SupplierService service,
        CancellationToken cancellationToken,
        bool includeDeleted = false)
    {
        var items = await service.QueryListAsync(includeDeleted, cancellationToken);
        return TypedResults.Ok(new SupplierListResponse { Total = items.Count, Page = 0, Size = items.Count, Items = items.Select(ToListResponseItem).ToList() });
    }

    private static async ValueTask<IResult> HandleGetAsync(
        SupplierService service,
        Guid id,
        CancellationToken cancellationToken)
    {
        var entity = await service.QueryAsync(id, cancellationToken);
        return entity is null ? ApiProblems.NotFound() : TypedResults.Ok(ToListResponseItem(entity));
    }

    private static async ValueTask<IResult> HandleCreateAsync(
        SupplierService service,
        SupplierCreateRequest request,
        CancellationToken cancellationToken)
    {
        var entity = ToEntity(request);
        var status = await service.InsertAsync(entity, cancellationToken);
        return status == DataWriteStatus.Success
            ? TypedResults.Created($"{ApiRoutes.Suppliers}/{entity.Id}", ToListResponseItem(entity))
            : ApiProblems.DuplicateCode();
    }

    private static async ValueTask<IResult> HandleUpdateAsync(
        SupplierService service,
        Guid id,
        SupplierUpdateRequest request,
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
        SupplierService service,
        Guid id,
        CancellationToken cancellationToken)
    {
        var status = await service.DeleteAsync(id, cancellationToken);
        return status == DataWriteStatus.Success ? TypedResults.NoContent() : ApiProblems.FromStatus(status);
    }
}
