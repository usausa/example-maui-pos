namespace Pos.Server.Host.Endpoints;

using Pos.Server.Accessors;
using Pos.Server.Host.Application;
using Pos.Server.Host.Infrastructure.Api;
using Pos.Server.Host.Mappers;
using Pos.Shared.TaxRates;

using Smart.Data;

public static class TaxRateEndpoints
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
    // Handler
    //--------------------------------------------------------------------------------

    // 少数なのでページングなし
    private static async ValueTask<IResult> HandleListAsync(
        TaxRateAccessor accessor,
        DateTime? updatedSince,
        CancellationToken cancellationToken,
        bool includeDeleted = false)
    {
        var items = await accessor.QueryListAsync(updatedSince, includeDeleted, cancellationToken);
        return TypedResults.Ok(new TaxRateListResponse { Total = items.Count, Page = 0, Size = items.Count, Items = items.Select(MasterMapper.ToTaxRateResponse).ToList() });
    }

    private static async ValueTask<IResult> HandleGetAsync(
        TaxRateAccessor accessor,
        Guid id,
        CancellationToken cancellationToken)
    {
        var entity = await accessor.QueryAsync(id, cancellationToken);
        return entity is null ? ApiProblems.NotFound() : TypedResults.Ok(MasterMapper.ToTaxRateResponse(entity));
    }

    private static async ValueTask<IResult> HandleCreateAsync(
        TaxRateAccessor accessor,
        IDialect dialect,
        TimeProvider timeProvider,
        TaxRateCreateRequest request,
        CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var entity = MasterMapper.ToTaxRateEntity(request);
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

        if (entity.IsDefault)
        {
            await accessor.ClearDefaultAsync(entity.Id, now, cancellationToken);
        }

        return TypedResults.Created($"{ApiRoutes.TaxRates}/{entity.Id}", MasterMapper.ToTaxRateResponse(entity));
    }

    private static async ValueTask<IResult> HandleUpdateAsync(
        TaxRateAccessor accessor,
        IDialect dialect,
        TimeProvider timeProvider,
        Guid id,
        TaxRateUpdateRequest request,
        CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;
        int rows;
        try
        {
            rows = await accessor.UpdateAsync(id, request.Code, request.Name, request.Rate, request.Kind, request.IsDefault, request.SortOrder, now, request.Version, cancellationToken);
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

        if (rows == 0)
        {
            return ApiProblems.VersionMismatch();
        }

        if (request.IsDefault)
        {
            await accessor.ClearDefaultAsync(id, now, cancellationToken);
        }

        return TypedResults.Ok(MasterMapper.ToTaxRateResponse(entity));
    }

    // 使用中の商品がある税率は削除できない
    private static async ValueTask<IResult> HandleDeleteAsync(
        TaxRateAccessor accessor,
        TimeProvider timeProvider,
        Guid id,
        CancellationToken cancellationToken)
    {
        if (await accessor.CountProductsAsync(id, cancellationToken) > 0)
        {
            return ApiProblems.InUse("使用中の商品がある税率は削除できません");
        }

        var rows = await accessor.DeleteAsync(id, timeProvider.GetUtcNow().UtcDateTime, cancellationToken);
        return rows == 0 ? ApiProblems.NotFound() : TypedResults.NoContent();
    }
}
