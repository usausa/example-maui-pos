namespace Pos.Server.Host.Endpoints;

using Pos.Server.Accessors;
using Pos.Server.Host.Application;
using Pos.Server.Host.Infrastructure.Api;
using Pos.Server.Host.Mappers;
using Pos.Shared.Discounts;

using Smart.Data;

public static class DiscountEndpoints
{
    //--------------------------------------------------------------------------------
    // Mapping
    //--------------------------------------------------------------------------------

    public static void MapDiscountEndpoints(this WebApplication app)
    {
        var group = app.MapGroup(ApiRoutes.Discounts);

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
        DiscountAccessor accessor,
        DateTime? updatedSince,
        CancellationToken cancellationToken,
        bool includeDeleted = false)
    {
        var items = await accessor.QueryListAsync(updatedSince, includeDeleted, cancellationToken);
        return TypedResults.Ok(new DiscountListResponse { Total = items.Count, Page = 0, Size = items.Count, Items = items.Select(MasterMapper.ToDiscountResponse).ToList() });
    }

    private static async ValueTask<IResult> HandleGetAsync(
        DiscountAccessor accessor,
        Guid id,
        CancellationToken cancellationToken)
    {
        var entity = await accessor.QueryAsync(id, cancellationToken);
        return entity is null ? ApiProblems.NotFound() : TypedResults.Ok(MasterMapper.ToDiscountResponse(entity));
    }

    private static async ValueTask<IResult> HandleCreateAsync(
        DiscountAccessor accessor,
        IDialect dialect,
        TimeProvider timeProvider,
        DiscountCreateRequest request,
        CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var entity = MasterMapper.ToDiscountEntity(request);
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

        return TypedResults.Created($"{ApiRoutes.Discounts}/{entity.Id}", MasterMapper.ToDiscountResponse(entity));
    }

    private static async ValueTask<IResult> HandleUpdateAsync(
        DiscountAccessor accessor,
        IDialect dialect,
        TimeProvider timeProvider,
        Guid id,
        DiscountUpdateRequest request,
        CancellationToken cancellationToken)
    {
        int rows;
        try
        {
            rows = await accessor.UpdateAsync(id, request.Code, request.Name, request.Type, request.Value, request.Scope, request.RequiresApproval, request.IsActive, request.SortOrder, timeProvider.GetUtcNow().UtcDateTime, request.Version, cancellationToken);
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

        return rows == 0 ? ApiProblems.VersionMismatch() : TypedResults.Ok(MasterMapper.ToDiscountResponse(entity));
    }

    private static async ValueTask<IResult> HandleDeleteAsync(
        DiscountAccessor accessor,
        TimeProvider timeProvider,
        Guid id,
        CancellationToken cancellationToken)
    {
        var rows = await accessor.DeleteAsync(id, timeProvider.GetUtcNow().UtcDateTime, cancellationToken);
        return rows == 0 ? ApiProblems.NotFound() : TypedResults.NoContent();
    }
}
