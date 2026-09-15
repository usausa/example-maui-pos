namespace Pos.Server.Host.Endpoints;

using Pos.Contract.Categories;
using Pos.Server.Host.Helpers;
using Pos.Server.Models.Entity;
using Pos.Server.Services;

using Smart.Mapper;

public static partial class CategoryEndpoints
{
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
    // Mapper
    //--------------------------------------------------------------------------------

    [Mapper]
    internal static partial CategoryResponseItem ToResponse(CategoryEntity entity);

    [Mapper]
    private static partial CategoryEntity ToEntity(CategoryCreateRequest request);

    [Mapper]
    private static partial CategoryEntity ToEntity(CategoryUpdateRequest request);

    //--------------------------------------------------------------------------------
    // Handler
    //--------------------------------------------------------------------------------

    private static async ValueTask<IResult> HandleListAsync(
        CategoryService service,
        DateTime? updatedSince,
        string? sort,
        CancellationToken cancellationToken,
        bool includeDeleted = false,
        bool desc = false,
        [Range(0, Int32.MaxValue)] int page = 0,
        [Range(1, ApiDefaults.MaxPageSize)] int size = ApiDefaults.MaxPageSize)
    {
        var result = await service.QueryPageAsync(updatedSince, includeDeleted, EnumHelper.Parse(sort, CategorySort.SortOrder), desc, page, size, cancellationToken);
        return TypedResults.Ok(new CategoryResponse { Total = result.Total, Page = result.Page, Size = result.Size, Items = result.Items.Select(ToResponse).ToList() });
    }

    private static async ValueTask<IResult> HandleGetAsync(
        CategoryService service,
        Guid id,
        CancellationToken cancellationToken)
    {
        var entity = await service.QueryAsync(id, cancellationToken);
        return entity is null ? ApiProblems.NotFound() : TypedResults.Ok(ToResponse(entity));
    }

    private static async ValueTask<IResult> HandleCreateAsync(
        CategoryService service,
        CategoryCreateRequest request,
        CancellationToken cancellationToken)
    {
        var entity = ToEntity(request);
        var status = await service.InsertAsync(entity, cancellationToken);
        return status == DataWriteStatus.Success
            ? TypedResults.Created($"{ApiRoutes.Categories}/{entity.Id}", ToResponse(entity))
            : ApiProblems.FromStatus(status, invalidTitle: "親部門に自分自身は指定できません");
    }

    private static async ValueTask<IResult> HandleUpdateAsync(
        CategoryService service,
        Guid id,
        CategoryUpdateRequest request,
        CancellationToken cancellationToken)
    {
        var entity = ToEntity(request);
        entity.Id = id;
        var result = await service.UpdateAsync(entity, cancellationToken);
        return result.Status == DataWriteStatus.Success
            ? TypedResults.Ok(ToResponse(result.Entity!))
            : ApiProblems.FromStatus(result.Status, invalidTitle: "親部門に自分自身は指定できません");
    }

    // 商品または子部門がある部門は削除できません
    private static async ValueTask<IResult> HandleDeleteAsync(
        CategoryService service,
        Guid id,
        CancellationToken cancellationToken)
    {
        var status = await service.DeleteAsync(id, cancellationToken);
        return status == DataWriteStatus.Success ? TypedResults.NoContent() : ApiProblems.FromStatus(status, inUseTitle: "商品または子部門がある部門は削除できません");
    }
}
