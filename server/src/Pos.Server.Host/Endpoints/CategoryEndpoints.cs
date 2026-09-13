namespace Pos.Server.Host.Endpoints;

using Pos.Domain.Rules;
using Pos.Server.Accessors;
using Pos.Server.Host.Application;
using Pos.Server.Host.Infrastructure.Api;
using Pos.Server.Host.Mappers;
using Pos.Shared.Categories;

using Smart.Data;

public static class CategoryEndpoints
{
    private static readonly string[] SortColumns = ["SortOrder", "Code", "Name", "UpdatedAt"];

    //--------------------------------------------------------------------------------
    // Mapping
    //--------------------------------------------------------------------------------

    public static void MapCategoryEndpoints(this WebApplication app)
    {
        var group = app.MapGroup(ApiRoutes.Categories);

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
        CategoryAccessor accessor,
        DateTime? updatedSince,
        string? sort,
        CancellationToken cancellationToken,
        bool includeDeleted = false,
        bool desc = false,
        [Range(0, Int32.MaxValue)] int page = 0,
        [Range(1, ApiHelper.MaxPageSize)] int size = ApiHelper.MaxPageSize)
    {
        var total = await accessor.CountAsync(updatedSince, includeDeleted, cancellationToken);
        var items = await accessor.QueryListAsync(updatedSince, includeDeleted, ApiHelper.ResolveSort(SortColumns, "SortOrder", sort, desc, updatedSince), size, page * size, cancellationToken);
        return TypedResults.Ok(new CategoryListResponse { Total = (int)total, Page = page, Size = size, Items = items.Select(MasterMapper.ToCategoryResponse).ToList() });
    }

    private static async ValueTask<IResult> HandleGetAsync(
        CategoryAccessor accessor,
        Guid id,
        CancellationToken cancellationToken)
    {
        var entity = await accessor.QueryAsync(id, cancellationToken);
        return entity is null ? ApiProblems.NotFound() : TypedResults.Ok(MasterMapper.ToCategoryResponse(entity));
    }

    private static async ValueTask<IResult> HandleCreateAsync(
        CategoryAccessor accessor,
        IDialect dialect,
        TimeProvider timeProvider,
        CategoryCreateRequest request,
        CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var entity = MasterMapper.ToCategoryEntity(request);
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

        return TypedResults.Created($"{ApiRoutes.Categories}/{entity.Id}", MasterMapper.ToCategoryResponse(entity));
    }

    private static async ValueTask<IResult> HandleUpdateAsync(
        CategoryAccessor accessor,
        IDialect dialect,
        TimeProvider timeProvider,
        Guid id,
        CategoryUpdateRequest request,
        CancellationToken cancellationToken)
    {
        if (request.ParentId == id)
        {
            return ApiProblems.Unprocessable(ErrorCode.ValidationError, "親部門に自分自身は指定できません");
        }

        int rows;
        try
        {
            rows = await accessor.UpdateAsync(id, request.Code, request.Name, request.ParentId, request.SortOrder, timeProvider.GetUtcNow().UtcDateTime, request.Version, cancellationToken);
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

        return rows == 0 ? ApiProblems.VersionMismatch() : TypedResults.Ok(MasterMapper.ToCategoryResponse(entity));
    }

    // 所属商品・子部門がある部門は削除できない
    private static async ValueTask<IResult> HandleDeleteAsync(
        CategoryAccessor accessor,
        TimeProvider timeProvider,
        Guid id,
        CancellationToken cancellationToken)
    {
        if ((await accessor.CountProductsAsync(id, cancellationToken) > 0) || (await accessor.CountChildrenAsync(id, cancellationToken) > 0))
        {
            return ApiProblems.InUse("商品または子部門がある部門は削除できません");
        }

        var rows = await accessor.DeleteAsync(id, timeProvider.GetUtcNow().UtcDateTime, cancellationToken);
        return rows == 0 ? ApiProblems.NotFound() : TypedResults.NoContent();
    }
}
