namespace Pos.Server.Host.Endpoints;

using Pos.Contract.TaxRates;
using Pos.Server.Models.Entity;
using Pos.Server.Services;

using Smart.Mapper;

public static partial class TaxRateEndpoints
{
    //--------------------------------------------------------------------------------
    // Mapping
    //--------------------------------------------------------------------------------

    public static void MapTaxRateEndpoints(this WebApplication app)
    {
        var group = app.MapApiGroup(ApiRoutes.TaxRates);
        group.MapGet("/", HandleListAsync)
            .WithName("TaxRateList")
            .Produces<TaxRateListResponse>();
        group.MapGet("/{id:guid}", HandleGetAsync)
            .WithName("TaxRateGet")
            .Produces<TaxRateListResponseItem>()
            .ProducesProblem(StatusCodes.Status404NotFound);
        group.MapPost("/", HandleCreateAsync)
            .RequireAuthorization(Policies.Administrator)
            .WithName("TaxRateCreate")
            .Produces<TaxRateListResponseItem>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .Produces(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status409Conflict);
        group.MapPut("/{id:guid}", HandleUpdateAsync)
            .RequireAuthorization(Policies.Administrator)
            .WithName("TaxRateUpdate")
            .Produces<TaxRateListResponseItem>()
            .ProducesValidationProblem()
            .Produces(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);
        group.MapDelete("/{id:guid}", HandleDeleteAsync)
            .RequireAuthorization(Policies.Administrator)
            .WithName("TaxRateDelete")
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
    internal static partial TaxRateListResponseItem ToListResponseItem(TaxRateEntity entity);

    [Mapper]
    private static partial TaxRateEntity ToEntity(TaxRateCreateRequest request);

    [Mapper]
    private static partial TaxRateEntity ToEntity(TaxRateUpdateRequest request);

    //--------------------------------------------------------------------------------
    // Handler
    //--------------------------------------------------------------------------------

    // 少数なのでページングなし
    private static async ValueTask<IResult> HandleListAsync(
        TaxRateService service,
        DateTime? updatedSince,
        CancellationToken cancellationToken,
        bool includeDeleted = false)
    {
        var items = await service.QueryListAsync(updatedSince, includeDeleted, cancellationToken);
        return TypedResults.Ok(new TaxRateListResponse { Total = items.Count, Page = 0, Size = items.Count, Items = items.Select(ToListResponseItem).ToList() });
    }

    private static async ValueTask<IResult> HandleGetAsync(
        TaxRateService service,
        Guid id,
        CancellationToken cancellationToken)
    {
        var entity = await service.QueryAsync(id, cancellationToken);
        return entity is null ? ApiProblems.NotFound() : TypedResults.Ok(ToListResponseItem(entity));
    }

    private static async ValueTask<IResult> HandleCreateAsync(
        TaxRateService service,
        TaxRateCreateRequest request,
        CancellationToken cancellationToken)
    {
        var entity = ToEntity(request);
        var status = await service.InsertAsync(entity, cancellationToken);
        return status == DataWriteStatus.Success
            ? TypedResults.Created($"{ApiRoutes.TaxRates}/{entity.Id}", ToListResponseItem(entity))
            : ApiProblems.DuplicateCode();
    }

    private static async ValueTask<IResult> HandleUpdateAsync(
        TaxRateService service,
        Guid id,
        TaxRateUpdateRequest request,
        CancellationToken cancellationToken)
    {
        var entity = ToEntity(request);
        entity.Id = id;
        var result = await service.UpdateAsync(entity, cancellationToken);
        return result.Status == DataWriteStatus.Success
            ? TypedResults.Ok(ToListResponseItem(result.Entity!))
            : ApiProblems.FromStatus(result.Status);
    }

    // 使用中の商品がある税率は削除できません
    private static async ValueTask<IResult> HandleDeleteAsync(
        TaxRateService service,
        Guid id,
        CancellationToken cancellationToken)
    {
        var status = await service.DeleteAsync(id, cancellationToken);
        return status == DataWriteStatus.Success ? TypedResults.NoContent() : ApiProblems.FromStatus(status, inUseTitle: "使用中の商品がある税率は削除できません");
    }
}
