namespace Pos.Server.Host.Endpoints;

using Microsoft.Net.Http.Headers;

using Pos.Contract.Products;
using Pos.Server.Host.Helpers;
using Pos.Server.Host.Infrastructure.Csv;
using Pos.Server.Host.Models.Export;
using Pos.Server.Host.Models.Import;
using Pos.Server.Infrastructure.Imaging;
using Pos.Server.Models.Entity;
using Pos.Server.Models.Parameters;
using Pos.Server.Models.Views;
using Pos.Server.Services;

using Smart.Mapper;

public static partial class ProductEndpoints
{
    private const string DuplicateTitle = "商品コードまたはバーコードが重複しています";

    private const string ImageInvalidTitle = "JPEG か PNG の画像を指定してください";

    //--------------------------------------------------------------------------------
    // Mapping
    //--------------------------------------------------------------------------------

    public static void MapProductEndpoints(this WebApplication app)
    {
        var group = app.MapApiGroup(ApiRoutes.Products);
        group.MapGet("/", HandleListAsync);
        group.MapGet("/lookup", HandleLookupAsync);
        group.MapGet("/csv", HandleExportCsvAsync).RequireAuthorization(Policies.Admin);
        group.MapGet("/{id:guid}", HandleGetAsync);
        group.MapPost("/", HandleCreateAsync).RequireAuthorization(Policies.Administrator);
        group.MapPut("/{id:guid}", HandleUpdateAsync).RequireAuthorization(Policies.Administrator);
        group.MapDelete("/{id:guid}", HandleDeleteAsync).RequireAuthorization(Policies.Administrator);
        group.MapGet("/{id:guid}/image", HandleGetImageAsync);
        group.MapPut("/{id:guid}/image", HandleSaveImageAsync).RequireAuthorization(Policies.Administrator);
        group.MapDelete("/{id:guid}/image", HandleDeleteImageAsync).RequireAuthorization(Policies.Administrator);
        group.MapPost("/import", HandleImportAsync).RequireAuthorization(Policies.Administrator);
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

    private static ProductImportResponse ToResponse(ProductImportResult result, bool dryRun) =>
        new()
        {
            DryRun = dryRun,
            InsertCount = result.InsertCount,
            UpdateCount = result.UpdateCount,
            UnchangedCount = result.UnchangedCount,
            ErrorCount = result.ErrorCount,
            Items = result.Lines.Select(static x => new ProductImportResponseItem
            {
                LineNo = x.LineNo,
                Code = x.Code,
                Name = x.Name,
                Action = x.Action,
                Errors = x.Errors.Select(ApiRuleText.Of).ToList()
            }).ToList()
        };

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

    //--------------------------------------------------------------------------------
    // Image
    //--------------------------------------------------------------------------------

    // v (内容のハッシュ) が今の画像と一致すれば内容は変わらないので長く持たせる。ETag で再検証もできる
    private static async ValueTask<IResult> HandleGetImageAsync(
        ProductService service,
        HttpResponse response,
        Guid id,
        string? v,
        CancellationToken cancellationToken)
    {
        var image = await service.QueryImageAsync(id, cancellationToken);
        if (image is null)
        {
            return ApiProblems.NotFound("画像がありません");
        }

        response.Headers.CacheControl = v == image.Version ? "private, max-age=31536000, immutable" : "no-cache";
        return TypedResults.Bytes(image.Data, image.ContentType, entityTag: new EntityTagHeaderValue($"\"{image.Version}\""));
    }

    // 本文に画像そのもの (Content-Type: image/jpeg / image/png、2 MB まで)。形式は先頭のバイトでも確かめる
    private static async ValueTask<IResult> HandleSaveImageAsync(
        ProductService service,
        HttpRequest request,
        Guid id,
        CancellationToken cancellationToken)
    {
        if (!RequestHelper.IsMediaType(request, ImageContentType.Jpeg, ImageContentType.Png))
        {
            return ApiProblems.UnsupportedMediaType(ImageInvalidTitle);
        }

        var data = await RequestHelper.ReadBodyAsync(request, ProductService.ImageMaxBytes, cancellationToken);
        if (data is null)
        {
            return ApiProblems.PayloadTooLarge("画像は 2 MB までにしてください");
        }

        var result = await service.SaveImageAsync(id, data, ApiRoutes.ProductImage(id), cancellationToken);
        return result.Status == DataWriteStatus.Success
            ? TypedResults.Ok(ToResponse(result.Entity!))
            : ApiProblems.FromStatus(result.Status, invalidTitle: ImageInvalidTitle);
    }

    private static async ValueTask<IResult> HandleDeleteImageAsync(
        ProductService service,
        Guid id,
        CancellationToken cancellationToken)
    {
        var result = await service.DeleteImageAsync(id, cancellationToken);
        return result.Status == DataWriteStatus.Success ? TypedResults.NoContent() : ApiProblems.FromStatus(result.Status);
    }

    //--------------------------------------------------------------------------------
    // Import
    //--------------------------------------------------------------------------------

    // 本文に CSV (Content-Type: text/csv。列は GET /products/csv と同じ)。
    // dryRun は検証だけで誤りのある行も 200 で返す。反映は誤りが 1 行でもあれば 422 (errors は行番号ごとの文言) で何も変えない
    private static async ValueTask<IResult> HandleImportAsync(
        ProductService service,
        HttpRequest request,
        CancellationToken cancellationToken,
        bool dryRun = false)
    {
        if (!RequestHelper.IsMediaType(request, "text/csv"))
        {
            return ApiProblems.UnsupportedMediaType("CSV (text/csv) を送ってください");
        }

        var data = await RequestHelper.ReadBodyAsync(request, CsvImport.MaxBytes, cancellationToken);
        if (data is null)
        {
            return ApiProblems.PayloadTooLarge("CSV は 5 MB までにしてください");
        }

        var csv = CsvImport.Read<ProductImportRow>(data);
        if (csv.MissingHeaders.Count > 0)
        {
            return ApiProblems.BadRequest($"CSV の列が足りません: {String.Join(", ", csv.MissingHeaders)}");
        }

        if (csv.Rows.Count == 0)
        {
            return ApiProblems.BadRequest("取り込む行がありません");
        }

        var result = await service.ImportAsync(ProductImportRow.ToLines(csv.Rows), dryRun, cancellationToken);
        if ((result.Status == DataWriteStatus.Success) || dryRun)
        {
            return TypedResults.Ok(ToResponse(result, dryRun));
        }

        if (result.Status == DataWriteStatus.Invalid)
        {
            var errors = result.Lines
                .Where(static x => x.Errors.Count > 0)
                .ToDictionary(static x => x.LineNo.ToString(CultureInfo.InvariantCulture), static x => x.Errors.Select(ApiRuleText.Of).ToArray(), StringComparer.Ordinal);
            return ApiProblems.Problem(StatusCodes.Status422UnprocessableEntity, ErrorCode.ValidationError, "取り込めない行があります", $"{errors.Count} 行に誤りがあります", errors);
        }

        return ApiProblems.VersionMismatch();
    }
}
