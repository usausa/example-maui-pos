namespace Pos.Server.Host.Endpoints;

using Pos.Domain.Rules;
using Pos.Server.Accessors;
using Pos.Server.Host.Application;
using Pos.Server.Host.Infrastructure.Api;
using Pos.Server.Host.Mappers;
using Pos.Server.Models.Entity;
using Pos.Shared.Customers;
using Pos.Shared.Transactions;

using Smart.Data;

public static class CustomerEndpoints
{
    private static readonly string[] SortColumns = ["Code", "Name", "Kana", "PointBalance", "CreatedAt", "UpdatedAt"];

    //--------------------------------------------------------------------------------
    // Mapping
    //--------------------------------------------------------------------------------

    public static void MapCustomerEndpoints(this WebApplication app)
    {
        var group = app.MapGroup(ApiRoutes.Customers);

        group.MapGet("/", HandleListAsync);
        group.MapGet("/lookup", HandleLookupAsync);
        group.MapGet("/{id:guid}", HandleGetAsync);
        group.MapPost("/", HandleCreateAsync);
        group.MapPut("/{id:guid}", HandleUpdateAsync);
        group.MapDelete("/{id:guid}", HandleDeleteAsync);
        group.MapGet("/{id:guid}/points/history", HandlePointHistoryAsync);
        group.MapPost("/{id:guid}/points/adjust", HandlePointAdjustAsync);
        group.MapGet("/{id:guid}/transactions", HandleTransactionsAsync);
    }

    //--------------------------------------------------------------------------------
    // Handler
    //--------------------------------------------------------------------------------

    // keyword は code / name / kana / phone の部分一致
    private static async ValueTask<IResult> HandleListAsync(
        CustomerAccessor accessor,
        IDialect dialect,
        string? keyword,
        string? code,
        string? phone,
        DateTime? updatedSince,
        string? sort,
        CancellationToken cancellationToken,
        bool includeDeleted = false,
        bool desc = false,
        [Range(0, Int32.MaxValue)] int page = 0,
        [Range(1, ApiHelper.MaxPageSize)] int size = ApiHelper.DefaultPageSize)
    {
        var pattern = ApiHelper.ToLikePattern(dialect, keyword);
        var total = await accessor.CountAsync(pattern, code, phone, updatedSince, includeDeleted, cancellationToken);
        var items = await accessor.QueryListAsync(pattern, code, phone, updatedSince, includeDeleted, ApiHelper.ResolveSort(SortColumns, "Code", sort, desc, updatedSince), size, page * size, cancellationToken);
        return TypedResults.Ok(new CustomerListResponse { Total = (int)total, Page = page, Size = size, Items = items.Select(MasterMapper.ToCustomerResponse).ToList() });
    }

    // 会員証スキャン用 1 件取得
    private static async ValueTask<IResult> HandleLookupAsync(
        CustomerAccessor accessor,
        string? code,
        CancellationToken cancellationToken)
    {
        if (String.IsNullOrEmpty(code))
        {
            return ApiProblems.Problem(StatusCodes.Status400BadRequest, ErrorCode.ValidationError, "code を指定してください");
        }

        var entity = await accessor.QueryByCodeAsync(code, cancellationToken);
        return entity is null ? ApiProblems.NotFound("会員が見つかりません") : TypedResults.Ok(MasterMapper.ToCustomerResponse(entity));
    }

    private static async ValueTask<IResult> HandleGetAsync(
        CustomerAccessor accessor,
        Guid id,
        CancellationToken cancellationToken)
    {
        var entity = await accessor.QueryAsync(id, cancellationToken);
        return entity is null ? ApiProblems.NotFound() : TypedResults.Ok(MasterMapper.ToCustomerResponse(entity));
    }

    private static async ValueTask<IResult> HandleCreateAsync(
        CustomerAccessor accessor,
        IDialect dialect,
        TimeProvider timeProvider,
        CustomerCreateRequest request,
        CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var entity = MasterMapper.ToCustomerEntity(request);
        entity.Id = Guid.CreateVersion7();
        entity.PointBalance = 0;
        entity.CreatedAt = now;
        entity.UpdatedAt = now;
        entity.Version = 1;

        try
        {
            await accessor.InsertAsync(entity, cancellationToken);
        }
        catch (DbException ex) when (dialect.IsDuplicate(ex))
        {
            return ApiProblems.DuplicateCode("会員番号が重複しています");
        }

        return TypedResults.Created($"{ApiRoutes.Customers}/{entity.Id}", MasterMapper.ToCustomerResponse(entity));
    }

    private static async ValueTask<IResult> HandleUpdateAsync(
        CustomerAccessor accessor,
        IDialect dialect,
        TimeProvider timeProvider,
        Guid id,
        CustomerUpdateRequest request,
        CancellationToken cancellationToken)
    {
        int rows;
        try
        {
            rows = await accessor.UpdateAsync(id, request.Code, request.Name, request.Kana, request.Phone, request.Email, request.PostalCode, request.Address, request.BirthDate, request.Note, timeProvider.GetUtcNow().UtcDateTime, request.Version, cancellationToken);
        }
        catch (DbException ex) when (dialect.IsDuplicate(ex))
        {
            return ApiProblems.DuplicateCode("会員番号が重複しています");
        }

        var entity = await accessor.QueryAsync(id, cancellationToken);
        if ((entity is null) || entity.IsDeleted)
        {
            return ApiProblems.NotFound();
        }

        return rows == 0 ? ApiProblems.VersionMismatch() : TypedResults.Ok(MasterMapper.ToCustomerResponse(entity));
    }

    private static async ValueTask<IResult> HandleDeleteAsync(
        CustomerAccessor accessor,
        TimeProvider timeProvider,
        Guid id,
        CancellationToken cancellationToken)
    {
        var rows = await accessor.DeleteAsync(id, timeProvider.GetUtcNow().UtcDateTime, cancellationToken);
        return rows == 0 ? ApiProblems.NotFound() : TypedResults.NoContent();
    }

    private static async ValueTask<IResult> HandlePointHistoryAsync(
        CustomerAccessor accessor,
        Guid id,
        CancellationToken cancellationToken,
        [Range(0, Int32.MaxValue)] int page = 0,
        [Range(1, ApiHelper.MaxPageSize)] int size = ApiHelper.DefaultPageSize)
    {
        if (await accessor.QueryAsync(id, cancellationToken) is null)
        {
            return ApiProblems.NotFound();
        }

        var total = await accessor.CountPointHistoryAsync(id, cancellationToken);
        var items = await accessor.QueryPointHistoryListAsync(id, size, page * size, cancellationToken);
        return TypedResults.Ok(new PointHistoryListResponse { Total = (int)total, Page = page, Size = size, Items = items.Select(MasterMapper.ToPointHistoryResponse).ToList() });
    }

    // 手動調整 (Adjust 履歴を作り、残高を加減算する)
    private static async ValueTask<IResult> HandlePointAdjustAsync(
        CustomerAccessor accessor,
        IDbProvider provider,
        TimeProvider timeProvider,
        Guid id,
        PointAdjustRequest request,
        CancellationToken cancellationToken)
    {
        if (await accessor.QueryAsync(id, cancellationToken) is null)
        {
            return ApiProblems.NotFound();
        }

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var history = await provider.UsingTxAsync(async (_, tx) =>
        {
            var balance = await accessor.AddPointsAsync(tx, id, request.Points, now, cancellationToken);
            var entity = new PointHistoryEntity
            {
                Id = Guid.CreateVersion7(),
                CustomerId = id,
                Type = PointHistoryType.Adjust,
                Points = request.Points,
                BalanceAfter = balance,
                Reason = request.Reason,
                StaffId = request.StaffId,
                OccurredAt = now,
                CreatedAt = now
            };
            await accessor.InsertPointHistoryAsync(tx, entity, cancellationToken);
            await tx.CommitAsync(cancellationToken);
            return entity;
        }, cancellationToken);

        return TypedResults.Ok(MasterMapper.ToPointHistoryResponse(history));
    }

    // 購入履歴 (新しい順)
    private static async ValueTask<IResult> HandleTransactionsAsync(
        CustomerAccessor accessor,
        TransactionAccessor transactionAccessor,
        Guid id,
        CancellationToken cancellationToken,
        [Range(0, Int32.MaxValue)] int page = 0,
        [Range(1, ApiHelper.MaxPageSize)] int size = ApiHelper.DefaultPageSize)
    {
        if (await accessor.QueryAsync(id, cancellationToken) is null)
        {
            return ApiProblems.NotFound();
        }

        var total = await transactionAccessor.CountAsync(null, null, null, null, id, null, null, null, null, cancellationToken);
        var entities = await transactionAccessor.QueryListAsync(null, null, null, null, id, null, null, null, null, "TransactedAt DESC", size, page * size, cancellationToken);
        var items = new List<TransactionResponse>(entities.Count);
        foreach (var entity in entities)
        {
            items.Add(await TransactionMapper.ToResponseAsync(transactionAccessor, entity, cancellationToken));
        }

        return TypedResults.Ok(new TransactionListResponse { Total = (int)total, Page = page, Size = size, Items = items });
    }
}
