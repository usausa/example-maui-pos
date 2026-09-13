namespace Pos.Server.Host.Endpoints;

using Pos.Server.Accessors;
using Pos.Server.Host.Application;
using Pos.Server.Host.Infrastructure.Api;
using Pos.Server.Host.Mappers;
using Pos.Shared.Terminals;

using Smart.Data;

public static class TerminalEndpoints
{
    private static readonly string[] SortColumns = ["TerminalNo", "Name", "UpdatedAt"];

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
    // Handler
    //--------------------------------------------------------------------------------

    private static async ValueTask<IResult> HandleListAsync(
        TerminalAccessor accessor,
        Guid? storeId,
        DateTime? updatedSince,
        string? sort,
        CancellationToken cancellationToken,
        bool includeDeleted = false,
        bool desc = false,
        [Range(0, Int32.MaxValue)] int page = 0,
        [Range(1, ApiHelper.MaxPageSize)] int size = ApiHelper.DefaultPageSize)
    {
        var total = await accessor.CountAsync(storeId, updatedSince, includeDeleted, cancellationToken);
        var items = await accessor.QueryListAsync(storeId, updatedSince, includeDeleted, ApiHelper.ResolveSort(SortColumns, "TerminalNo", sort, desc, updatedSince), size, page * size, cancellationToken);
        return TypedResults.Ok(new TerminalListResponse { Total = (int)total, Page = page, Size = size, Items = items.Select(MasterMapper.ToTerminalResponse).ToList() });
    }

    private static async ValueTask<IResult> HandleGetAsync(
        TerminalAccessor accessor,
        Guid id,
        CancellationToken cancellationToken)
    {
        var entity = await accessor.QueryAsync(id, cancellationToken);
        return entity is null ? ApiProblems.NotFound() : TypedResults.Ok(MasterMapper.ToTerminalResponse(entity));
    }

    private static async ValueTask<IResult> HandleCreateAsync(
        TerminalAccessor accessor,
        IDialect dialect,
        TimeProvider timeProvider,
        TerminalCreateRequest request,
        CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var entity = MasterMapper.ToTerminalEntity(request);
        entity.Id = Guid.CreateVersion7();
        entity.CreatedAt = now;
        entity.UpdatedAt = now;
        entity.Version = 1;

        try
        {
            await accessor.InsertAsync(entity, cancellationToken);
        }
        catch (DbException ex) when (dialect.IsDuplicate(ex))
        {
            return ApiProblems.DuplicateCode("端末番号が重複しています");
        }

        return TypedResults.Created($"{ApiRoutes.Terminals}/{entity.Id}", MasterMapper.ToTerminalResponse(entity));
    }

    private static async ValueTask<IResult> HandleUpdateAsync(
        TerminalAccessor accessor,
        IDialect dialect,
        TimeProvider timeProvider,
        Guid id,
        TerminalUpdateRequest request,
        CancellationToken cancellationToken)
    {
        int rows;
        try
        {
            rows = await accessor.UpdateAsync(id, request.StoreId, request.TerminalNo, request.Name, request.IsActive, timeProvider.GetUtcNow().UtcDateTime, request.Version, cancellationToken);
        }
        catch (DbException ex) when (dialect.IsDuplicate(ex))
        {
            return ApiProblems.DuplicateCode("端末番号が重複しています");
        }

        var entity = await accessor.QueryAsync(id, cancellationToken);
        if ((entity is null) || entity.IsDeleted)
        {
            return ApiProblems.NotFound();
        }

        return rows == 0 ? ApiProblems.VersionMismatch() : TypedResults.Ok(MasterMapper.ToTerminalResponse(entity));
    }

    // 開設中シフトがある端末は削除できない
    private static async ValueTask<IResult> HandleDeleteAsync(
        TerminalAccessor accessor,
        TimeProvider timeProvider,
        Guid id,
        CancellationToken cancellationToken)
    {
        if (await accessor.CountOpenShiftAsync(id, cancellationToken) > 0)
        {
            return ApiProblems.InUse("開設中のシフトがある端末は削除できません");
        }

        var rows = await accessor.DeleteAsync(id, timeProvider.GetUtcNow().UtcDateTime, cancellationToken);
        return rows == 0 ? ApiProblems.NotFound() : TypedResults.NoContent();
    }
}
