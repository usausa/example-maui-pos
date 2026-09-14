namespace Pos.Server.Host.Endpoints;

using Pos.Contract.Inventory;
using Pos.Server.Models.Entity;
using Pos.Server.Models.Parameters;
using Pos.Server.Models.Views;
using Pos.Server.Services;

using Smart.Mapper;

// 在庫。取引による変動はサーバが自動生成し、端末からは棚卸・調整だけを送る
public static partial class InventoryEndpoints
{
    //--------------------------------------------------------------------------------
    // Mapping
    //--------------------------------------------------------------------------------

    public static void MapInventoryEndpoints(this WebApplication app)
    {
        var group = app.MapGroup(ApiRoutes.Inventory);
        group.MapGet("/", HandleLevelListAsync);
        group.MapPost("/changes", HandleChangesAsync);
        group.MapGet("/changes", HandleChangeListAsync);
        group.MapGet("/adjustment-reasons", HandleReasonListAsync);
        group.MapGet("/adjustment-reasons/{id:guid}", HandleReasonGetAsync);
        group.MapPost("/adjustment-reasons", HandleReasonCreateAsync);
        group.MapPut("/adjustment-reasons/{id:guid}", HandleReasonUpdateAsync);
        group.MapDelete("/adjustment-reasons/{id:guid}", HandleReasonDeleteAsync);
        group.MapGet("/{productId:guid}", HandleProductLevelsAsync);
    }

    //--------------------------------------------------------------------------------
    // Mapper
    //--------------------------------------------------------------------------------

    [Mapper]
    private static partial InventoryLevelResponseItem ToResponse(InventoryLevelEntity entity);

    [Mapper]
    private static partial ProductInventoryResponseLevel ToResponse(ProductInventoryLevel level);

    [Mapper]
    private static partial InventoryChangeResponseItem ToResponse(InventoryChangeEntity entity);

    [Mapper]
    private static partial InventoryChangeParameter ToParameter(InventoryChangeRequestChange change);

    [Mapper]
    internal static partial AdjustmentReasonResponseItem ToResponse(AdjustmentReasonEntity entity);

    [Mapper]
    private static partial AdjustmentReasonEntity ToEntity(AdjustmentReasonCreateRequest request);

    [Mapper]
    private static partial AdjustmentReasonEntity ToEntity(AdjustmentReasonUpdateRequest request);

    //--------------------------------------------------------------------------------
    // Level
    //--------------------------------------------------------------------------------

    private static async ValueTask<IResult> HandleLevelListAsync(
        InventoryService service,
        Guid? storeId,
        Guid? productId,
        Guid? categoryId,
        DateTime? updatedSince,
        CancellationToken cancellationToken,
        bool negativeOnly = false,
        [Range(0, Int32.MaxValue)] int page = 0,
        [Range(1, ApiDefaults.MaxPageSize)] int size = ApiDefaults.PageSize)
    {
        var parameter = new InventoryLevelQueryParameter
        {
            StoreId = storeId,
            ProductId = productId,
            CategoryId = categoryId,
            NegativeOnly = negativeOnly,
            UpdatedSince = updatedSince,
            Page = page,
            Size = size
        };
        var result = await service.QueryLevelPageAsync(parameter, cancellationToken);
        return TypedResults.Ok(new InventoryLevelResponse { Total = result.Total, Page = result.Page, Size = result.Size, Items = result.Items.Select(ToResponse).ToList() });
    }

    // 商品の全店舗在庫 (他店在庫照会)
    private static async ValueTask<IResult> HandleProductLevelsAsync(
        InventoryService service,
        Guid productId,
        CancellationToken cancellationToken)
    {
        var levels = await service.QueryProductLevelsAsync(productId, cancellationToken);
        return levels is null
            ? ApiProblems.NotFound("商品が見つかりません")
            : TypedResults.Ok(new ProductInventoryResponse { ProductId = productId, Levels = levels.Select(ToResponse).ToList() });
    }

    //--------------------------------------------------------------------------------
    // Change
    //--------------------------------------------------------------------------------

    // 棚卸 (絶対数量) と調整 (増減) の一括登録。同じ id は Duplicate として既存の結果を返す
    private static async ValueTask<IResult> HandleChangesAsync(
        InventoryService service,
        InventoryChangeRequest request,
        CancellationToken cancellationToken)
    {
        var results = await service.ApplyChangesAsync(request.Changes.Select(ToParameter).ToList(), cancellationToken);
        return TypedResults.Ok(new InventoryChangeResultResponse
        {
            Results = results.Select(static x => new InventoryChangeResultResponseResult
            {
                Id = x.Change.Id,
                Status = x.Duplicate ? InventoryChangeResultStatus.Duplicate : InventoryChangeResultStatus.Created,
                QuantityDelta = x.Change.QuantityDelta,
                QuantityAfter = x.Change.QuantityAfter
            }).ToList()
        });
    }

    // from / to は営業日ではなく UTC 日時 (to は含まない)
    private static async ValueTask<IResult> HandleChangeListAsync(
        InventoryService service,
        Guid? storeId,
        Guid? productId,
        InventoryChangeType? type,
        DateTime? from,
        DateTime? to,
        CancellationToken cancellationToken,
        [Range(0, Int32.MaxValue)] int page = 0,
        [Range(1, ApiDefaults.MaxPageSize)] int size = ApiDefaults.PageSize)
    {
        var parameter = new InventoryChangeQueryParameter { StoreId = storeId, ProductId = productId, Type = type, From = from, To = to, Page = page, Size = size };
        var result = await service.QueryChangePageAsync(parameter, cancellationToken);
        return TypedResults.Ok(new InventoryChangeResponse { Total = result.Total, Page = result.Page, Size = result.Size, Items = result.Items.Select(ToResponse).ToList() });
    }

    //--------------------------------------------------------------------------------
    // AdjustmentReason
    //--------------------------------------------------------------------------------

    // 少数なのでページングなし
    private static async ValueTask<IResult> HandleReasonListAsync(
        AdjustmentReasonService service,
        DateTime? updatedSince,
        CancellationToken cancellationToken,
        bool includeDeleted = false)
    {
        var items = await service.QueryListAsync(updatedSince, includeDeleted, cancellationToken);
        return TypedResults.Ok(new AdjustmentReasonResponse { Total = items.Count, Page = 0, Size = items.Count, Items = items.Select(ToResponse).ToList() });
    }

    private static async ValueTask<IResult> HandleReasonGetAsync(
        AdjustmentReasonService service,
        Guid id,
        CancellationToken cancellationToken)
    {
        var entity = await service.QueryAsync(id, cancellationToken);
        return entity is null ? ApiProblems.NotFound() : TypedResults.Ok(ToResponse(entity));
    }

    private static async ValueTask<IResult> HandleReasonCreateAsync(
        AdjustmentReasonService service,
        AdjustmentReasonCreateRequest request,
        CancellationToken cancellationToken)
    {
        var entity = ToEntity(request);
        var status = await service.InsertAsync(entity, cancellationToken);
        return status == DataWriteStatus.Success
            ? TypedResults.Created($"{ApiRoutes.Inventory}/adjustment-reasons/{entity.Id}", ToResponse(entity))
            : ApiProblems.DuplicateCode();
    }

    private static async ValueTask<IResult> HandleReasonUpdateAsync(
        AdjustmentReasonService service,
        Guid id,
        AdjustmentReasonUpdateRequest request,
        CancellationToken cancellationToken)
    {
        var entity = ToEntity(request);
        entity.Id = id;
        var status = await service.UpdateAsync(entity, cancellationToken);
        return status == DataWriteStatus.Success
            ? TypedResults.Ok(ToResponse((await service.QueryAsync(id, cancellationToken))!))
            : ApiProblems.FromStatus(status);
    }

    private static async ValueTask<IResult> HandleReasonDeleteAsync(
        AdjustmentReasonService service,
        Guid id,
        CancellationToken cancellationToken)
    {
        var status = await service.DeleteAsync(id, cancellationToken);
        return status == DataWriteStatus.Success ? TypedResults.NoContent() : ApiProblems.FromStatus(status);
    }
}
