namespace Pos.Server.Host.Endpoints;

using Pos.Server.Accessors;
using Pos.Server.Host.Application;
using Pos.Server.Host.Infrastructure.Api;
using Pos.Server.Host.Mappers;
using Pos.Shared.Staff;

using Smart.Data;

public static class StaffEndpoints
{
    private static readonly string[] SortColumns = ["Code", "Name", "UpdatedAt"];

    //--------------------------------------------------------------------------------
    // Mapping
    //--------------------------------------------------------------------------------

    public static void MapStaffEndpoints(this WebApplication app)
    {
        var group = app.MapGroup(ApiRoutes.Staff);

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
        StaffAccessor accessor,
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
        var items = await accessor.QueryListAsync(storeId, updatedSince, includeDeleted, ApiHelper.ResolveSort(SortColumns, "Code", sort, desc, updatedSince), size, page * size, cancellationToken);
        return TypedResults.Ok(new StaffListResponse { Total = (int)total, Page = page, Size = size, Items = items.Select(MasterMapper.ToStaffResponse).ToList() });
    }

    private static async ValueTask<IResult> HandleGetAsync(
        StaffAccessor accessor,
        Guid id,
        CancellationToken cancellationToken)
    {
        var entity = await accessor.QueryAsync(id, cancellationToken);
        return entity is null ? ApiProblems.NotFound() : TypedResults.Ok(MasterMapper.ToStaffResponse(entity));
    }

    private static async ValueTask<IResult> HandleCreateAsync(
        StaffAccessor accessor,
        IDialect dialect,
        TimeProvider timeProvider,
        StaffCreateRequest request,
        CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var entity = MasterMapper.ToStaffEntity(request);
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

        return TypedResults.Created($"{ApiRoutes.Staff}/{entity.Id}", MasterMapper.ToStaffResponse(entity));
    }

    private static async ValueTask<IResult> HandleUpdateAsync(
        StaffAccessor accessor,
        IDialect dialect,
        TimeProvider timeProvider,
        Guid id,
        StaffUpdateRequest request,
        CancellationToken cancellationToken)
    {
        int rows;
        try
        {
            rows = await accessor.UpdateAsync(id, request.Code, request.Name, request.Role, request.StoreId, request.IsActive, timeProvider.GetUtcNow().UtcDateTime, request.Version, cancellationToken);
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

        return rows == 0 ? ApiProblems.VersionMismatch() : TypedResults.Ok(MasterMapper.ToStaffResponse(entity));
    }

    private static async ValueTask<IResult> HandleDeleteAsync(
        StaffAccessor accessor,
        TimeProvider timeProvider,
        Guid id,
        CancellationToken cancellationToken)
    {
        var rows = await accessor.DeleteAsync(id, timeProvider.GetUtcNow().UtcDateTime, cancellationToken);
        return rows == 0 ? ApiProblems.NotFound() : TypedResults.NoContent();
    }
}
