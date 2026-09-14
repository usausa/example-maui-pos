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
        var group = app.MapGroup(ApiRoutes.TaxRates);
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
    internal static partial TaxRateResponseItem ToResponse(TaxRateEntity entity);

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
        return TypedResults.Ok(new TaxRateResponse { Total = items.Count, Page = 0, Size = items.Count, Items = items.Select(ToResponse).ToList() });
    }

    private static async ValueTask<IResult> HandleGetAsync(
        TaxRateService service,
        Guid id,
        CancellationToken cancellationToken)
    {
        var entity = await service.QueryAsync(id, cancellationToken);
        return entity is null ? ApiProblems.NotFound() : TypedResults.Ok(ToResponse(entity));
    }

    private static async ValueTask<IResult> HandleCreateAsync(
        TaxRateService service,
        TaxRateCreateRequest request,
        CancellationToken cancellationToken)
    {
        var entity = ToEntity(request);
        var status = await service.InsertAsync(entity, cancellationToken);
        return status == DataWriteStatus.Success
            ? TypedResults.Created($"{ApiRoutes.TaxRates}/{entity.Id}", ToResponse(entity))
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
        var status = await service.UpdateAsync(entity, cancellationToken);
        return status == DataWriteStatus.Success
            ? TypedResults.Ok(ToResponse((await service.QueryAsync(id, cancellationToken))!))
            : ApiProblems.FromStatus(status);
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
