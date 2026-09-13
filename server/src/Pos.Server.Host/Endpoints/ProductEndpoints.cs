namespace Pos.Server.Host.Endpoints;

using Pos.Domain.Rules;
using Pos.Server.Accessors;
using Pos.Server.Host.Application;
using Pos.Server.Host.Infrastructure.Api;
using Pos.Server.Host.Mappers;
using Pos.Shared.Products;

using Smart.Data;

public static class ProductEndpoints
{
    private static readonly string[] SortColumns = ["Code", "Name", "Price", "UpdatedAt"];

    //--------------------------------------------------------------------------------
    // Mapping
    //--------------------------------------------------------------------------------

    public static void MapProductEndpoints(this WebApplication app)
    {
        var group = app.MapGroup(ApiRoutes.Products);

        group.MapGet("/", HandleListAsync);
        group.MapGet("/lookup", HandleLookupAsync);
        group.MapGet("/{id:guid}", HandleGetAsync);
        group.MapPost("/", HandleCreateAsync);
        group.MapPut("/{id:guid}", HandleUpdateAsync);
        group.MapDelete("/{id:guid}", HandleDeleteAsync);
    }

    //--------------------------------------------------------------------------------
    // Handler
    //--------------------------------------------------------------------------------

    // keyword は code / barcode / name / kana / modelNo の部分一致
    private static async ValueTask<IResult> HandleListAsync(
        ProductAccessor accessor,
        IDialect dialect,
        Guid? categoryId,
        string? keyword,
        bool? isActive,
        DateTime? updatedSince,
        string? sort,
        CancellationToken cancellationToken,
        bool includeDeleted = false,
        bool desc = false,
        [Range(0, Int32.MaxValue)] int page = 0,
        [Range(1, ApiHelper.MaxPageSize)] int size = ApiHelper.DefaultPageSize)
    {
        var pattern = ApiHelper.ToLikePattern(dialect, keyword);
        var total = await accessor.CountAsync(categoryId, pattern, isActive, updatedSince, includeDeleted, cancellationToken);
        var items = await accessor.QueryListAsync(categoryId, pattern, isActive, updatedSince, includeDeleted, ApiHelper.ResolveSort(SortColumns, "Code", sort, desc, updatedSince), size, page * size, cancellationToken);
        return TypedResults.Ok(new ProductListResponse { Total = (int)total, Page = page, Size = size, Items = items.Select(MasterMapper.ToProductResponse).ToList() });
    }

    // スキャン用 1 件取得 (barcode または code)
    private static async ValueTask<IResult> HandleLookupAsync(
        ProductAccessor accessor,
        string? barcode,
        string? code,
        CancellationToken cancellationToken)
    {
        if (String.IsNullOrEmpty(barcode) && String.IsNullOrEmpty(code))
        {
            return ApiProblems.Problem(StatusCodes.Status400BadRequest, ErrorCode.ValidationError, "barcode または code を指定してください");
        }

        var entity = !String.IsNullOrEmpty(barcode)
            ? await accessor.QueryByBarcodeAsync(barcode, cancellationToken)
            : await accessor.QueryByCodeAsync(code!, cancellationToken);
        return entity is null ? ApiProblems.NotFound("商品が見つかりません") : TypedResults.Ok(MasterMapper.ToProductResponse(entity));
    }

    private static async ValueTask<IResult> HandleGetAsync(
        ProductAccessor accessor,
        Guid id,
        CancellationToken cancellationToken)
    {
        var entity = await accessor.QueryAsync(id, cancellationToken);
        return entity is null ? ApiProblems.NotFound() : TypedResults.Ok(MasterMapper.ToProductResponse(entity));
    }

    private static async ValueTask<IResult> HandleCreateAsync(
        ProductAccessor accessor,
        IDialect dialect,
        TimeProvider timeProvider,
        ProductCreateRequest request,
        CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var entity = MasterMapper.ToProductEntity(request);
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
            return ApiProblems.DuplicateCode("商品コードまたはバーコードが重複しています");
        }

        return TypedResults.Created($"{ApiRoutes.Products}/{entity.Id}", MasterMapper.ToProductResponse(entity));
    }

    private static async ValueTask<IResult> HandleUpdateAsync(
        ProductAccessor accessor,
        IDialect dialect,
        TimeProvider timeProvider,
        Guid id,
        ProductUpdateRequest request,
        CancellationToken cancellationToken)
    {
        int rows;
        try
        {
            rows = await accessor.UpdateAsync(
                id,
                request.Code,
                request.Barcode,
                request.Name,
                request.Kana,
                request.Brand,
                request.ModelNo,
                request.CategoryId,
                request.Kind,
                request.Price,
                request.TaxIncluded,
                request.TaxRateId,
                request.Cost,
                request.PointRate,
                request.RequiresSerial,
                request.TrackInventory,
                request.AllowsPriceOverride,
                request.Unit,
                request.IsActive,
                timeProvider.GetUtcNow().UtcDateTime,
                request.Version,
                cancellationToken);
        }
        catch (DbException ex) when (dialect.IsDuplicate(ex))
        {
            return ApiProblems.DuplicateCode("商品コードまたはバーコードが重複しています");
        }

        var entity = await accessor.QueryAsync(id, cancellationToken);
        if ((entity is null) || entity.IsDeleted)
        {
            return ApiProblems.NotFound();
        }

        return rows == 0 ? ApiProblems.VersionMismatch() : TypedResults.Ok(MasterMapper.ToProductResponse(entity));
    }

    private static async ValueTask<IResult> HandleDeleteAsync(
        ProductAccessor accessor,
        TimeProvider timeProvider,
        Guid id,
        CancellationToken cancellationToken)
    {
        var rows = await accessor.DeleteAsync(id, timeProvider.GetUtcNow().UtcDateTime, cancellationToken);
        return rows == 0 ? ApiProblems.NotFound() : TypedResults.NoContent();
    }
}
