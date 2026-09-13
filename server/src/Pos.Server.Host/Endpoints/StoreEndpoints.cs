namespace Pos.Server.Host.Endpoints;

using Pos.Server.Accessors;
using Pos.Server.Host.Application;
using Pos.Server.Host.Infrastructure.Api;
using Pos.Server.Host.Mappers;
using Pos.Shared.Stores;

using Smart.Data;

public static class StoreEndpoints
{
    private static readonly string[] SortColumns = ["Code", "Name", "UpdatedAt"];

    //--------------------------------------------------------------------------------
    // Mapping
    //--------------------------------------------------------------------------------

    public static void MapStoreEndpoints(this WebApplication app)
    {
        var group = app.MapGroup(ApiRoutes.Stores);

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
        StoreAccessor accessor,
        DateTime? updatedSince,
        string? sort,
        CancellationToken cancellationToken,
        bool includeDeleted = false,
        bool desc = false,
        [Range(0, Int32.MaxValue)] int page = 0,
        [Range(1, ApiHelper.MaxPageSize)] int size = ApiHelper.DefaultPageSize)
    {
        var total = await accessor.CountAsync(updatedSince, includeDeleted, cancellationToken);
        var items = await accessor.QueryListAsync(updatedSince, includeDeleted, ApiHelper.ResolveSort(SortColumns, "Code", sort, desc, updatedSince), size, page * size, cancellationToken);
        return TypedResults.Ok(new StoreListResponse { Total = (int)total, Page = page, Size = size, Items = items.Select(MasterMapper.ToStoreResponse).ToList() });
    }

    private static async ValueTask<IResult> HandleGetAsync(
        StoreAccessor accessor,
        Guid id,
        CancellationToken cancellationToken)
    {
        var entity = await accessor.QueryAsync(id, cancellationToken);
        return entity is null ? ApiProblems.NotFound() : TypedResults.Ok(MasterMapper.ToStoreResponse(entity));
    }

    private static async ValueTask<IResult> HandleCreateAsync(
        StoreAccessor accessor,
        IDialect dialect,
        TimeProvider timeProvider,
        StoreCreateRequest request,
        CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var entity = MasterMapper.ToStoreEntity(request);
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
            return ApiProblems.DuplicateCode();
        }

        return TypedResults.Created($"{ApiRoutes.Stores}/{entity.Id}", MasterMapper.ToStoreResponse(entity));
    }

    private static async ValueTask<IResult> HandleUpdateAsync(
        StoreAccessor accessor,
        IDialect dialect,
        TimeProvider timeProvider,
        Guid id,
        StoreUpdateRequest request,
        CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;
        int rows;
        try
        {
            rows = await accessor.UpdateAsync(
                id,
                request.Code,
                request.Name,
                request.PostalCode,
                request.Address,
                request.Phone,
                request.RegistrationNo,
                request.ReceiptHeader,
                request.ReceiptFooter,
                request.TimeZone,
                request.IsActive,
                now,
                request.Version,
                cancellationToken);
        }
        catch (DbException ex) when (dialect.IsDuplicate(ex))
        {
            return ApiProblems.DuplicateCode();
        }

        var entity = await accessor.QueryAsync(id, cancellationToken);
        if ((entity is null) || entity.IsDeleted)
        {
            return ApiProblems.NotFound();
        }

        return rows == 0 ? ApiProblems.VersionMismatch() : TypedResults.Ok(MasterMapper.ToStoreResponse(entity));
    }

    // 端末・在庫が残っている店舗は削除できない
    private static async ValueTask<IResult> HandleDeleteAsync(
        StoreAccessor accessor,
        TerminalAccessor terminalAccessor,
        InventoryAccessor inventoryAccessor,
        TimeProvider timeProvider,
        Guid id,
        CancellationToken cancellationToken)
    {
        if ((await terminalAccessor.CountAsync(id, null, false, cancellationToken) > 0) ||
            (await inventoryAccessor.CountLevelsAsync(id, null, null, false, null, cancellationToken) > 0))
        {
            return ApiProblems.InUse("端末または在庫がある店舗は削除できません");
        }

        var rows = await accessor.DeleteAsync(id, timeProvider.GetUtcNow().UtcDateTime, cancellationToken);
        return rows == 0 ? ApiProblems.NotFound() : TypedResults.NoContent();
    }
}
