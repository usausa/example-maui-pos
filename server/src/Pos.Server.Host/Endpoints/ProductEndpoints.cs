namespace Pos.Server.Host.Endpoints;

using Pos.Contract.Products;
using Pos.Server.Host.Helpers;
using Pos.Server.Host.Infrastructure.Csv;
using Pos.Server.Host.Models.Export;
using Pos.Server.Models.Entity;
using Pos.Server.Models.Parameters;
using Pos.Server.Models.Views;
using Pos.Server.Services;

using Smart.Mapper;

public static partial class ProductEndpoints
{
    private const string DuplicateTitle = "商品コードまたはバーコードが重複しています";

    //--------------------------------------------------------------------------------
    // Mapping
    //--------------------------------------------------------------------------------

    public static void MapProductEndpoints(this WebApplication app)
    {
        var group = app.MapGroup(ApiRoutes.Products);
        group.MapGet("/", HandleListAsync);
        group.MapGet("/lookup", HandleLookupAsync);
        group.MapGet("/csv", HandleExportCsvAsync);
        group.MapGet("/{id:guid}", HandleGetAsync);
        group.MapPost("/", HandleCreateAsync);
        group.MapPut("/{id:guid}", HandleUpdateAsync);
        group.MapDelete("/{id:guid}", HandleDeleteAsync);
    }

    //--------------------------------------------------------------------------------
    // Mapper
    //--------------------------------------------------------------------------------

    [Mapper]
    internal static partial ProductResponseItem ToResponse(ProductEntity entity);

    [Mapper]
    private static partial ProductEntity ToEntity(ProductCreateRequest request);

    [Mapper]
    private static partial ProductEntity ToEntity(ProductUpdateRequest request);

    [Mapper]
    private static partial ProductExportRow ToExportRow(ProductExportView item);

    //--------------------------------------------------------------------------------
    // Handler
    //--------------------------------------------------------------------------------

    // keyword は code / barcode / name / kana / modelNo の部分一致
    private static async ValueTask<IResult> HandleListAsync(
        ProductService service,
        Guid? categoryId,
        string? keyword,
        bool? isActive,
        DateTime? updatedSince,
        string? sort,
        CancellationToken cancellationToken,
        bool includeDeleted = false,
        bool desc = false,
        [Range(0, Int32.MaxValue)] int page = 0,
        [Range(1, ApiDefaults.MaxPageSize)] int size = ApiDefaults.PageSize)
    {
        var parameter = new ProductQueryParameter
        {
            CategoryId = categoryId,
            Keyword = keyword,
            IsActive = isActive,
            UpdatedSince = updatedSince,
            IncludeDeleted = includeDeleted,
            Sort = EnumHelper.Parse(sort, ProductSort.Code),
            Desc = desc,
            Page = page,
            Size = size
        };
        var result = await service.QueryPageAsync(parameter, cancellationToken);
        return TypedResults.Ok(new ProductResponse { Total = result.Total, Page = result.Page, Size = result.Size, Items = result.Items.Select(ToResponse).ToList() });
    }

    // CSV 出力 (削除済みを除く全件、コード順)
    private static async ValueTask<IResult> HandleExportCsvAsync(
        ProductService service,
        CancellationToken cancellationToken)
    {
        var items = await service.QueryExportListAsync(cancellationToken);
        return CsvExport.Stream(items.Select(ToExportRow), "products.csv");
    }

    // スキャン用 1 件取得 (barcode または code)
    private static async ValueTask<IResult> HandleLookupAsync(
        ProductService service,
        string? barcode,
        string? code,
        CancellationToken cancellationToken)
    {
        if (String.IsNullOrEmpty(barcode) && String.IsNullOrEmpty(code))
        {
            return ApiProblems.BadRequest("barcode または code を指定してください");
        }

        var entity = !String.IsNullOrEmpty(barcode)
            ? await service.QueryByBarcodeAsync(barcode, cancellationToken)
            : await service.QueryByCodeAsync(code!, cancellationToken);
        return entity is null ? ApiProblems.NotFound("商品が見つかりません") : TypedResults.Ok(ToResponse(entity));
    }

    private static async ValueTask<IResult> HandleGetAsync(
        ProductService service,
        Guid id,
        CancellationToken cancellationToken)
    {
        var entity = await service.QueryAsync(id, cancellationToken);
        return entity is null ? ApiProblems.NotFound() : TypedResults.Ok(ToResponse(entity));
    }

    private static async ValueTask<IResult> HandleCreateAsync(
        ProductService service,
        ProductCreateRequest request,
        CancellationToken cancellationToken)
    {
        var entity = ToEntity(request);
        var status = await service.InsertAsync(entity, cancellationToken);
        return status == DataWriteStatus.Success
            ? TypedResults.Created($"{ApiRoutes.Products}/{entity.Id}", ToResponse(entity))
            : ApiProblems.DuplicateCode(DuplicateTitle);
    }

    private static async ValueTask<IResult> HandleUpdateAsync(
        ProductService service,
        Guid id,
        ProductUpdateRequest request,
        CancellationToken cancellationToken)
    {
        var entity = ToEntity(request);
        entity.Id = id;
        var result = await service.UpdateAsync(entity, cancellationToken);
        return result.Status == DataWriteStatus.Success
            ? TypedResults.Ok(ToResponse(result.Entity!))
            : ApiProblems.FromStatus(result.Status, duplicateTitle: DuplicateTitle);
    }

    private static async ValueTask<IResult> HandleDeleteAsync(
        ProductService service,
        Guid id,
        CancellationToken cancellationToken)
    {
        var status = await service.DeleteAsync(id, cancellationToken);
        return status == DataWriteStatus.Success ? TypedResults.NoContent() : ApiProblems.FromStatus(status);
    }
}
