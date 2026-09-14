namespace Pos.Server.Host.Endpoints;

using Pos.Contract.Terminals;
using Pos.Server.Models.Entity;
using Pos.Server.Services;

using Smart.Mapper;

public static partial class TerminalEndpoints
{
    //--------------------------------------------------------------------------------
    // Mapping
    //--------------------------------------------------------------------------------

    public static void MapTerminalEndpoints(this WebApplication app)
    {
        var group = app.MapGroup(ApiRoutes.Terminals);
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
    internal static partial TerminalResponseItem ToResponse(TerminalEntity entity);

    [Mapper]
    private static partial TerminalEntity ToEntity(TerminalCreateRequest request);

    [Mapper]
    private static partial TerminalEntity ToEntity(TerminalUpdateRequest request);

    //--------------------------------------------------------------------------------
    // Handler
    //--------------------------------------------------------------------------------

    private static async ValueTask<IResult> HandleListAsync(
        TerminalService service,
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
        return TypedResults.Ok(new TerminalResponse { Total = result.Total, Page = result.Page, Size = result.Size, Items = result.Items.Select(ToResponse).ToList() });
    }

    private static async ValueTask<IResult> HandleGetAsync(
        TerminalService service,
        Guid id,
        CancellationToken cancellationToken)
    {
        var entity = await service.QueryAsync(id, cancellationToken);
        return entity is null ? ApiProblems.NotFound() : TypedResults.Ok(ToResponse(entity));
    }

    private static async ValueTask<IResult> HandleCreateAsync(
        TerminalService service,
        TerminalCreateRequest request,
        CancellationToken cancellationToken)
    {
        var entity = ToEntity(request);
        var status = await service.InsertAsync(entity, cancellationToken);
        return status == DataWriteStatus.Success
            ? TypedResults.Created($"{ApiRoutes.Terminals}/{entity.Id}", ToResponse(entity))
            : ApiProblems.DuplicateCode("端末番号が重複しています");
    }

    private static async ValueTask<IResult> HandleUpdateAsync(
        TerminalService service,
        Guid id,
        TerminalUpdateRequest request,
        CancellationToken cancellationToken)
    {
        var entity = ToEntity(request);
        entity.Id = id;
        var status = await service.UpdateAsync(entity, cancellationToken);
        return status == DataWriteStatus.Success
            ? TypedResults.Ok(ToResponse((await service.QueryAsync(id, cancellationToken))!))
            : ApiProblems.FromStatus(status, duplicateTitle: "端末番号が重複しています");
    }

    // 開設中のシフトがある端末は削除できません
    private static async ValueTask<IResult> HandleDeleteAsync(
        TerminalService service,
        Guid id,
        CancellationToken cancellationToken)
    {
        var status = await service.DeleteAsync(id, cancellationToken);
        return status == DataWriteStatus.Success ? TypedResults.NoContent() : ApiProblems.FromStatus(status, inUseTitle: "開設中のシフトがある端末は削除できません");
    }
}
