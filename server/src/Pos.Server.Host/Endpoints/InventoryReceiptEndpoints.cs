namespace Pos.Server.Host.Endpoints;

using Pos.Contract.InventoryReceipts;
using Pos.Server.Host.Helpers;
using Pos.Server.Models.Entity;
using Pos.Server.Models.Parameters;
using Pos.Server.Models.Views;
using Pos.Server.Services;

using Smart.Mapper;

// 入荷 (入荷予定の登録と受領)
public static partial class InventoryReceiptEndpoints
{
    //--------------------------------------------------------------------------------
    // Mapping
    //--------------------------------------------------------------------------------

    public static void MapInventoryReceiptEndpoints(this WebApplication app)
    {
        var group = app.MapApiGroup(ApiRoutes.InventoryReceipts);
        group.MapGet("/", HandleListAsync)
            .WithName("InventoryReceiptList")
            .Produces<InventoryReceiptListResponse>()
            .ProducesValidationProblem();
        group.MapGet("/{id:guid}", HandleGetAsync)
            .WithName("InventoryReceiptGet")
            .Produces<InventoryReceiptListResponseItem>()
            .ProducesProblem(StatusCodes.Status404NotFound);
        group.MapPost("/", HandleCreateAsync)
            .RequireAuthorization(Policies.Admin)
            .WithName("InventoryReceiptCreate")
            .Produces<InventoryReceiptListResponseItem>()
            .Produces<InventoryReceiptListResponseItem>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .Produces(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);
        group.MapPost("/{id:guid}/cancel", HandleCancelAsync)
            .RequireAuthorization(Policies.Admin)
            .WithName("InventoryReceiptCancel")
            .Produces<InventoryReceiptListResponseItem>()
            .Produces(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);
        group.MapPost("/{id:guid}/receive", HandleReceiveAsync)
            .WithName("InventoryReceiptReceive")
            .Produces<InventoryReceiptListResponseItem>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);
    }

    //--------------------------------------------------------------------------------
    // Mapper
    //--------------------------------------------------------------------------------

    [Mapper]
    private static partial InventoryReceiptEntity ToEntity(InventoryReceiptCreateRequest request);

    [Mapper]
    private static partial InventoryReceiptLineEntity ToEntity(InventoryReceiptCreateRequestLine request);

    [Mapper]
    private static partial InventoryReceiptListResponseItem ToListResponseItemCore(InventoryReceiptEntity entity);

    [Mapper]
    private static partial InventoryReceiptListResponseLine ToListResponseLine(InventoryReceiptLineEntity entity);

    private static InventoryReceiptListResponseItem ToListResponseItem(InventoryReceiptDetailView detail)
    {
        var response = ToListResponseItemCore(detail.Receipt);
        response.SupplierName = detail.SupplierName;
        response.PurchaseOrderId = detail.PurchaseOrder?.Id;
        response.PurchaseOrderNo = detail.PurchaseOrder?.PurchaseOrderNo;
        response.Lines = detail.Lines.Select(ToListResponseLine).ToList();
        return response;
    }

    private static IResult ToResult(InventoryReceiptResult result) =>
        result.Status switch
        {
            InventoryReceiptResultStatus.Success => TypedResults.Ok(ToListResponseItem(result.Detail!)),
            InventoryReceiptResultStatus.NotFound => ApiProblems.NotFound(),
            _ => ApiProblems.FromViolation(result.Violation!)
        };

    //--------------------------------------------------------------------------------
    // Handler
    //--------------------------------------------------------------------------------

    // from / to は入荷予定日
    private static async ValueTask<IResult> HandleListAsync(
        InventoryReceiptService service,
        Guid? storeId,
        Guid? supplierId,
        InventoryReceiptStatus? status,
        DateOnly? from,
        DateOnly? to,
        string? sort,
        CancellationToken cancellationToken,
        bool desc = true,
        [Range(0, Int32.MaxValue)] int page = 0,
        [Range(1, ApiDefaults.MaxPageSize)] int size = ApiDefaults.PageSize)
    {
        var parameter = new InventoryReceiptQueryParameter
        {
            StoreId = storeId,
            SupplierId = supplierId,
            Status = status,
            From = from,
            To = to,
            Sort = EnumHelper.Parse(sort, InventoryReceiptSort.CreatedAt),
            Desc = desc,
            Page = page,
            Size = size
        };
        var result = await service.QueryPageAsync(parameter, cancellationToken);
        return TypedResults.Ok(new InventoryReceiptListResponse { Total = result.Total, Page = result.Page, Size = result.Size, Items = result.Items.Select(ToListResponseItem).ToList() });
    }

    private static async ValueTask<IResult> HandleGetAsync(
        InventoryReceiptService service,
        Guid id,
        CancellationToken cancellationToken)
    {
        var detail = await service.QueryDetailAsync(id, cancellationToken);
        return detail is null ? ApiProblems.NotFound() : TypedResults.Ok(ToListResponseItem(detail));
    }

    private static async ValueTask<IResult> HandleCreateAsync(
        InventoryReceiptService service,
        InventoryReceiptCreateRequest request,
        CancellationToken cancellationToken)
    {
        var result = await service.CreateAsync(ToEntity(request), request.Lines.Select(ToEntity).ToList(), cancellationToken);
        return result.Status == InventoryReceiptResultStatus.Success
            ? TypedResults.Created($"{ApiRoutes.InventoryReceipts}/{result.Detail!.Receipt.Id}", ToListResponseItem(result.Detail))
            : ToResult(result);
    }

    // 受領 (入荷予定のときだけ)。明細を省略すると予定の数で受け取る
    // 端末の受領は入荷の店舗が自店であること
    private static async ValueTask<IResult> HandleReceiveAsync(
        TerminalAccess access,
        InventoryReceiptService service,
        ClaimsPrincipal user,
        Guid id,
        InventoryReceiptReceiveRequest request,
        CancellationToken cancellationToken)
    {
        if ((await service.QueryDetailAsync(id, cancellationToken) is { } detail) && !access.CanAccess(user, detail.Receipt.StoreId))
        {
            return ApiProblems.TerminalMismatch();
        }

        var quantities = request.Lines.ToDictionary(static x => x.LineId, static x => x.Quantity);
        return ToResult(await service.ReceiveAsync(id, request.StaffId, request.ReceivedAt, quantities, cancellationToken));
    }

    private static async ValueTask<IResult> HandleCancelAsync(
        InventoryReceiptService service,
        Guid id,
        CancellationToken cancellationToken)
    {
        return ToResult(await service.CancelAsync(id, cancellationToken));
    }
}
