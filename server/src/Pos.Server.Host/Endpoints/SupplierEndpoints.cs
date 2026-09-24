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
        var group = app.MapApiGroup(ApiRoutes.Suppliers);
        group.MapGet("/", HandleListAsync);
        group.MapGet("/{id:guid}", HandleGetAsync);
        // 認証の導入時: 仕入先の登録・更新・削除はマスタの書き込みなので Administrator に限る
        group.MapPost("/", HandleCreateAsync);
        group.MapPut("/{id:guid}", HandleUpdateAsync);
        group.MapDelete("/{id:guid}", HandleDeleteAsync);
    }

    //--------------------------------------------------------------------------------
    // Mapper
    //--------------------------------------------------------------------------------

    [Mapper]
    private static partial SupplierResponseItem ToResponse(SupplierEntity entity);

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
        return TypedResults.Ok(new SupplierResponse { Total = items.Count, Page = 0, Size = items.Count, Items = items.Select(ToResponse).ToList() });
    }

    private static async ValueTask<IResult> HandleGetAsync(
        SupplierService service,
        Guid id,
        CancellationToken cancellationToken)
    {
        var entity = await service.QueryAsync(id, cancellationToken);
        return entity is null ? ApiProblems.NotFound() : TypedResults.Ok(ToResponse(entity));
    }

    private static async ValueTask<IResult> HandleCreateAsync(
        SupplierService service,
        SupplierCreateRequest request,
        CancellationToken cancellationToken)
    {
        var entity = ToEntity(request);
        var status = await service.InsertAsync(entity, cancellationToken);
        return status == DataWriteStatus.Success
            ? TypedResults.Created($"{ApiRoutes.Suppliers}/{entity.Id}", ToResponse(entity))
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
            ? TypedResults.Ok(ToResponse(result.Entity!))
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
