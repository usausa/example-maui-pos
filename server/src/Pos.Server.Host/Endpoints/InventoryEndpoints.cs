namespace Pos.Server.Host.Endpoints;

using Pos.Server.Accessors;
using Pos.Server.Host.Application;
using Pos.Server.Host.Infrastructure.Api;
using Pos.Server.Host.Infrastructure.Data;
using Pos.Server.Host.Mappers;
using Pos.Server.Models.Entity;
using Pos.Shared.Inventory;

using Smart.Data;

// 在庫 (api-design §3.14)。取引による変動はサーバが自動生成し、端末からは棚卸・調整だけを送る
public static class InventoryEndpoints
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
    // Level
    //--------------------------------------------------------------------------------

    private static async ValueTask<IResult> HandleLevelListAsync(
        InventoryAccessor accessor,
        Guid? storeId,
        Guid? productId,
        Guid? categoryId,
        DateTime? updatedSince,
        CancellationToken cancellationToken,
        bool negativeOnly = false,
        [Range(0, Int32.MaxValue)] int page = 0,
        [Range(1, ApiHelper.MaxPageSize)] int size = ApiHelper.DefaultPageSize)
    {
        // 差分同期は updatedAt 順、通常は商品コード順
        var order = updatedSince is not null ? "i.UpdatedAt, i.StoreId, i.ProductId" : "p.Code, i.StoreId";
        var total = await accessor.CountLevelsAsync(storeId, productId, categoryId, negativeOnly, updatedSince, cancellationToken);
        var items = await accessor.QueryLevelListAsync(storeId, productId, categoryId, negativeOnly, updatedSince, order, size, page * size, cancellationToken);
        return TypedResults.Ok(new InventoryLevelListResponse { Total = (int)total, Page = page, Size = size, Items = items.Select(InventoryMapper.ToLevelResponse).ToList() });
    }

    // 商品の全店舗在庫 (他店在庫照会)
    private static async ValueTask<IResult> HandleProductLevelsAsync(
        InventoryAccessor accessor,
        ProductAccessor productAccessor,
        Guid productId,
        CancellationToken cancellationToken)
    {
        if (await productAccessor.QueryAsync(productId, cancellationToken) is null)
        {
            return ApiProblems.NotFound("商品が見つかりません");
        }

        var levels = await accessor.QueryLevelsByProductAsync(productId, cancellationToken);
        return TypedResults.Ok(new ProductInventoryResponse { ProductId = productId, Levels = levels.Select(InventoryMapper.ToProductLevel).ToList() });
    }

    //--------------------------------------------------------------------------------
    // Change
    //--------------------------------------------------------------------------------

    // 棚卸 (絶対数量) と調整 (増減) の一括登録。同じ id は Duplicate として既存の結果を返す
    private static async ValueTask<IResult> HandleChangesAsync(
        InventoryAccessor accessor,
        IDbProvider provider,
        TimeProvider timeProvider,
        InventoryChangeRequest request,
        CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var results = new List<InventoryChangeResultResponseResult>(request.Changes.Count);

        foreach (var change in request.Changes)
        {
            var existing = await accessor.QueryChangeAsync(change.Id, cancellationToken);
            if (existing is not null)
            {
                results.Add(new InventoryChangeResultResponseResult { Id = change.Id, Status = InventoryChangeResultStatus.Duplicate, QuantityDelta = existing.QuantityDelta, QuantityAfter = existing.QuantityAfter });
                continue;
            }

            var entity = await InventoryChangeApplier.ApplyAsync(
                accessor,
                provider,
                new InventoryChangeEntity
                {
                    Id = change.Id,
                    StoreId = change.StoreId,
                    ProductId = change.ProductId,
                    Type = change.Type,
                    ReasonId = change.ReasonId,
                    Reason = change.Reason,
                    StaffId = change.StaffId,
                    OccurredAt = change.OccurredAt
                },
                change.Quantity,
                now,
                cancellationToken);

            results.Add(new InventoryChangeResultResponseResult { Id = entity.Id, Status = InventoryChangeResultStatus.Created, QuantityDelta = entity.QuantityDelta, QuantityAfter = entity.QuantityAfter });
        }

        return TypedResults.Ok(new InventoryChangeResultResponse { Results = results });
    }

    // from / to は営業日ではなく UTC 日時 (to は含まない)
    private static async ValueTask<IResult> HandleChangeListAsync(
        InventoryAccessor accessor,
        Guid? storeId,
        Guid? productId,
        InventoryChangeType? type,
        DateTime? from,
        DateTime? to,
        CancellationToken cancellationToken,
        [Range(0, Int32.MaxValue)] int page = 0,
        [Range(1, ApiHelper.MaxPageSize)] int size = ApiHelper.DefaultPageSize)
    {
        var total = await accessor.CountChangesAsync(storeId, productId, type, from, to, cancellationToken);
        var items = await accessor.QueryChangeListAsync(storeId, productId, type, from, to, size, page * size, cancellationToken);
        return TypedResults.Ok(new InventoryChangeListResponse { Total = (int)total, Page = page, Size = size, Items = items.Select(InventoryMapper.ToChangeResponse).ToList() });
    }

    //--------------------------------------------------------------------------------
    // AdjustmentReason
    //--------------------------------------------------------------------------------

    private static async ValueTask<IResult> HandleReasonListAsync(
        AdjustmentReasonAccessor accessor,
        DateTime? updatedSince,
        CancellationToken cancellationToken,
        bool includeDeleted = false)
    {
        var items = await accessor.QueryListAsync(updatedSince, includeDeleted, cancellationToken);
        return TypedResults.Ok(new AdjustmentReasonListResponse { Total = items.Count, Page = 0, Size = items.Count, Items = items.Select(MasterMapper.ToAdjustmentReasonResponse).ToList() });
    }

    private static async ValueTask<IResult> HandleReasonGetAsync(
        AdjustmentReasonAccessor accessor,
        Guid id,
        CancellationToken cancellationToken)
    {
        var entity = await accessor.QueryAsync(id, cancellationToken);
        return entity is null ? ApiProblems.NotFound() : TypedResults.Ok(MasterMapper.ToAdjustmentReasonResponse(entity));
    }

    private static async ValueTask<IResult> HandleReasonCreateAsync(
        AdjustmentReasonAccessor accessor,
        IDialect dialect,
        TimeProvider timeProvider,
        AdjustmentReasonCreateRequest request,
        CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var entity = MasterMapper.ToAdjustmentReasonEntity(request);
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

        return TypedResults.Created($"{ApiRoutes.Inventory}/adjustment-reasons/{entity.Id}", MasterMapper.ToAdjustmentReasonResponse(entity));
    }

    private static async ValueTask<IResult> HandleReasonUpdateAsync(
        AdjustmentReasonAccessor accessor,
        IDialect dialect,
        TimeProvider timeProvider,
        Guid id,
        AdjustmentReasonUpdateRequest request,
        CancellationToken cancellationToken)
    {
        int rows;
        try
        {
            rows = await accessor.UpdateAsync(id, request.Code, request.Name, request.SortOrder, request.IsActive, timeProvider.GetUtcNow().UtcDateTime, request.Version, cancellationToken);
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

        return rows == 0 ? ApiProblems.VersionMismatch() : TypedResults.Ok(MasterMapper.ToAdjustmentReasonResponse(entity));
    }

    private static async ValueTask<IResult> HandleReasonDeleteAsync(
        AdjustmentReasonAccessor accessor,
        TimeProvider timeProvider,
        Guid id,
        CancellationToken cancellationToken)
    {
        var rows = await accessor.DeleteAsync(id, timeProvider.GetUtcNow().UtcDateTime, cancellationToken);
        return rows == 0 ? ApiProblems.NotFound() : TypedResults.NoContent();
    }
}
