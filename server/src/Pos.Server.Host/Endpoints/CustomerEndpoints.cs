namespace Pos.Server.Host.Endpoints;

using Pos.Contract.Customers;
using Pos.Contract.Transactions;
using Pos.Server.Host.Helpers;
using Pos.Server.Models.Entity;
using Pos.Server.Models.Parameters;
using Pos.Server.Services;

using Smart.Mapper;

public static partial class CustomerEndpoints
{
    private const string DuplicateTitle = "会員番号が重複しています";

    //--------------------------------------------------------------------------------
    // Mapping
    //--------------------------------------------------------------------------------

    public static void MapCustomerEndpoints(this WebApplication app)
    {
        var group = app.MapApiGroup(ApiRoutes.Customers);
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
    // Mapper
    //--------------------------------------------------------------------------------

    [Mapper]
    internal static partial CustomerResponseItem ToResponse(CustomerEntity entity);

    [Mapper]
    private static partial CustomerEntity ToEntity(CustomerCreateRequest request);

    [Mapper]
    private static partial CustomerEntity ToEntity(CustomerUpdateRequest request);

    [Mapper]
    private static partial CustomerPointHistoryResponseItem ToResponse(PointHistoryEntity entity);

    //--------------------------------------------------------------------------------
    // Customer
    //--------------------------------------------------------------------------------

    // keyword は code / name / kana / phone の部分一致
    private static async ValueTask<IResult> HandleListAsync(
        CustomerService service,
        string? keyword,
        string? code,
        string? phone,
        DateTime? updatedSince,
        string? sort,
        CancellationToken cancellationToken,
        bool includeDeleted = false,
        bool desc = false,
        [Range(0, Int32.MaxValue)] int page = 0,
        [Range(1, ApiDefaults.MaxPageSize)] int size = ApiDefaults.PageSize)
    {
        var parameter = new CustomerQueryParameter
        {
            Keyword = keyword,
            Code = code,
            Phone = phone,
            UpdatedSince = updatedSince,
            IncludeDeleted = includeDeleted,
            Sort = EnumHelper.Parse(sort, CustomerSort.Code),
            Desc = desc,
            Page = page,
            Size = size
        };
        var result = await service.QueryPageAsync(parameter, cancellationToken);
        return TypedResults.Ok(new CustomerResponse { Total = result.Total, Page = result.Page, Size = result.Size, Items = result.Items.Select(ToResponse).ToList() });
    }

    // 会員証スキャン用 1 件取得
    private static async ValueTask<IResult> HandleLookupAsync(
        CustomerService service,
        string? code,
        CancellationToken cancellationToken)
    {
        if (String.IsNullOrEmpty(code))
        {
            return ApiProblems.BadRequest("code を指定してください");
        }

        var entity = await service.QueryByCodeAsync(code, cancellationToken);
        return entity is null ? ApiProblems.NotFound("会員が見つかりません") : TypedResults.Ok(ToResponse(entity));
    }

    private static async ValueTask<IResult> HandleGetAsync(
        CustomerService service,
        Guid id,
        CancellationToken cancellationToken)
    {
        var entity = await service.QueryAsync(id, cancellationToken);
        return entity is null ? ApiProblems.NotFound() : TypedResults.Ok(ToResponse(entity));
    }

    private static async ValueTask<IResult> HandleCreateAsync(
        CustomerService service,
        CustomerCreateRequest request,
        CancellationToken cancellationToken)
    {
        var entity = ToEntity(request);
        var status = await service.InsertAsync(entity, cancellationToken);
        return status == DataWriteStatus.Success
            ? TypedResults.Created($"{ApiRoutes.Customers}/{entity.Id}", ToResponse(entity))
            : ApiProblems.DuplicateCode(DuplicateTitle);
    }

    private static async ValueTask<IResult> HandleUpdateAsync(
        CustomerService service,
        Guid id,
        CustomerUpdateRequest request,
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
        CustomerService service,
        Guid id,
        CancellationToken cancellationToken)
    {
        var status = await service.DeleteAsync(id, cancellationToken);
        return status == DataWriteStatus.Success ? TypedResults.NoContent() : ApiProblems.FromStatus(status);
    }

    //--------------------------------------------------------------------------------
    // Points
    //--------------------------------------------------------------------------------

    private static async ValueTask<IResult> HandlePointHistoryAsync(
        CustomerService service,
        Guid id,
        CancellationToken cancellationToken,
        [Range(0, Int32.MaxValue)] int page = 0,
        [Range(1, ApiDefaults.MaxPageSize)] int size = ApiDefaults.PageSize)
    {
        var result = await service.QueryPointHistoryPageAsync(id, page, size, cancellationToken);
        return result is null
            ? ApiProblems.NotFound()
            : TypedResults.Ok(new CustomerPointHistoryResponse { Total = result.Total, Page = result.Page, Size = result.Size, Items = result.Items.Select(ToResponse).ToList() });
    }

    // 手動調整 (Adjust 履歴を作り、残高を加減算する)
    private static async ValueTask<IResult> HandlePointAdjustAsync(
        CustomerService service,
        Guid id,
        CustomerPointAdjustRequest request,
        CancellationToken cancellationToken)
    {
        var history = await service.AdjustPointsAsync(id, request.Points, request.Reason, request.StaffId, cancellationToken);
        return history is null ? ApiProblems.NotFound() : TypedResults.Ok(ToResponse(history));
    }

    // 購入履歴 (新しい順)
    private static async ValueTask<IResult> HandleTransactionsAsync(
        CustomerService service,
        TransactionService transactionService,
        Guid id,
        CancellationToken cancellationToken,
        [Range(0, Int32.MaxValue)] int page = 0,
        [Range(1, ApiDefaults.MaxPageSize)] int size = ApiDefaults.PageSize)
    {
        if (await service.QueryAsync(id, cancellationToken) is null)
        {
            return ApiProblems.NotFound();
        }

        var result = await transactionService.QueryDetailPageAsync(new TransactionQueryParameter { CustomerId = id, Desc = true, Page = page, Size = size }, cancellationToken);
        return TypedResults.Ok(new TransactionResponse { Total = result.Total, Page = result.Page, Size = result.Size, Items = result.Items.Select(static x => TransactionEndpoints.ToResponse(x)).ToList() });
    }
}
