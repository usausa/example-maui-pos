namespace Pos.Server.Host.Endpoints;

using Pos.Contract.PurchaseOrders;
using Pos.Server.Host.Helpers;
using Pos.Server.Host.Reports;
using Pos.Server.Models.Entity;
using Pos.Server.Models.Parameters;
using Pos.Server.Models.Views;
using Pos.Server.Services;

using Smart.Mapper;

// 発注 (仕入先への注文。[発注] で入荷予定を作る)
public static partial class PurchaseOrderEndpoints
{
    //--------------------------------------------------------------------------------
    // Mapping
    //--------------------------------------------------------------------------------

    public static void MapPurchaseOrderEndpoints(this WebApplication app)
    {
        // 発注は管理画面だけで使う (端末は発注で作った入荷予定を検品する)
        var group = app.MapApiGroup(ApiRoutes.PurchaseOrders).RequireAuthorization(Policies.Admin);
        group.MapGet("/", HandleListAsync);
        group.MapGet("/{id:guid}", HandleGetAsync);
        group.MapGet("/{id:guid}/pdf", HandlePdfAsync);
        group.MapPost("/", HandleCreateAsync);
        group.MapPut("/{id:guid}", HandleUpdateAsync);
        group.MapPost("/{id:guid}/order", HandleOrderAsync);
        group.MapPost("/{id:guid}/cancel", HandleCancelAsync);
    }

    //--------------------------------------------------------------------------------
    // Mapper
    //--------------------------------------------------------------------------------

    [Mapper]
    private static partial PurchaseOrderEntity ToEntity(PurchaseOrderCreateRequest request);

    [Mapper]
    private static partial PurchaseOrderLineEntity ToEntity(PurchaseOrderCreateRequestLine request);

    [Mapper]
    private static partial PurchaseOrderLineEntity ToEntity(PurchaseOrderUpdateRequestLine request);

    [Mapper]
    private static partial PurchaseOrderResponseItem ToResponseCore(PurchaseOrderEntity entity);

    [Mapper]
    private static partial PurchaseOrderResponseLine ToResponseCore(PurchaseOrderLineEntity entity);

    private static PurchaseOrderUpdateParameter ToParameter(PurchaseOrderUpdateRequest request) =>
        new()
        {
            SupplierId = request.SupplierId,
            ExpectedDate = request.ExpectedDate,
            Note = request.Note,
            Lines = request.Lines.Select(ToEntity).ToList(),
            Version = request.Version
        };

    private static PurchaseOrderResponseItem ToResponse(PurchaseOrderDetailView detail)
    {
        var response = ToResponseCore(detail.PurchaseOrder);
        response.SupplierName = detail.SupplierName;
        response.TotalCost = detail.TotalCost;
        response.Lines = detail.Lines.Select(x =>
        {
            var line = ToResponseCore(x);
            line.ReceivedQuantity = detail.ReceivedQuantityOf(x);
            return line;
        }).ToList();
        return response;
    }

    private static IResult ToResult(PurchaseOrderResult result) =>
        result.Status switch
        {
            PurchaseOrderResultStatus.Success => TypedResults.Ok(ToResponse(result.Detail!)),
            PurchaseOrderResultStatus.NotFound => ApiProblems.NotFound(),
            PurchaseOrderResultStatus.VersionMismatch => ApiProblems.VersionMismatch(),
            _ => ApiProblems.FromViolation(result.Violation!)
        };

    //--------------------------------------------------------------------------------
    // Handler
    //--------------------------------------------------------------------------------

    // from / to は希望納期。open は未完了 (下書き・発注済み) だけ
    private static async ValueTask<IResult> HandleListAsync(
        PurchaseOrderService service,
        Guid? storeId,
        Guid? supplierId,
        PurchaseOrderStatus? status,
        DateOnly? from,
        DateOnly? to,
        string? sort,
        CancellationToken cancellationToken,
        bool open = false,
        bool desc = true,
        [Range(0, Int32.MaxValue)] int page = 0,
        [Range(1, ApiDefaults.MaxPageSize)] int size = ApiDefaults.PageSize)
    {
        var parameter = new PurchaseOrderQueryParameter
        {
            StoreId = storeId,
            SupplierId = supplierId,
            Status = status,
            OpenOnly = open,
            From = from,
            To = to,
            Sort = EnumHelper.Parse(sort, PurchaseOrderSort.CreatedAt),
            Desc = desc,
            Page = page,
            Size = size
        };
        var result = await service.QueryPageAsync(parameter, cancellationToken);
        return TypedResults.Ok(new PurchaseOrderResponse { Total = result.Total, Page = result.Page, Size = result.Size, Items = result.Items.Select(ToResponse).ToList() });
    }

    private static async ValueTask<IResult> HandleGetAsync(
        PurchaseOrderService service,
        Guid id,
        CancellationToken cancellationToken)
    {
        var detail = await service.QueryDetailAsync(id, cancellationToken);
        return detail is null ? ApiProblems.NotFound() : TypedResults.Ok(ToResponse(detail));
    }

    // 発注書 (仕入先へは PDF を人が送る)
    private static async ValueTask<IResult> HandlePdfAsync(
        PurchaseOrderService service,
        PurchaseOrderReportBuilder reportBuilder,
        Guid id,
        CancellationToken cancellationToken)
    {
        var report = await service.QueryReportAsync(id, cancellationToken);
        if (report is null)
        {
            return ApiProblems.NotFound();
        }

        var bytes = reportBuilder.Build(report);
        return TypedResults.File(bytes, "application/pdf", $"purchase-order-{report.Detail.PurchaseOrder.PurchaseOrderNo}.pdf");
    }

    private static async ValueTask<IResult> HandleCreateAsync(
        PurchaseOrderService service,
        PurchaseOrderCreateRequest request,
        CancellationToken cancellationToken)
    {
        var result = await service.CreateAsync(ToEntity(request), request.Lines.Select(ToEntity).ToList(), cancellationToken);
        return result.Status == PurchaseOrderResultStatus.Success
            ? TypedResults.Created($"{ApiRoutes.PurchaseOrders}/{result.Detail!.PurchaseOrder.Id}", ToResponse(result.Detail))
            : ToResult(result);
    }

    // 変更 (下書きのときだけ)。明細は置き換える
    private static async ValueTask<IResult> HandleUpdateAsync(
        PurchaseOrderService service,
        Guid id,
        PurchaseOrderUpdateRequest request,
        CancellationToken cancellationToken)
    {
        return ToResult(await service.UpdateAsync(id, ToParameter(request), cancellationToken));
    }

    // 発注 (下書きのときだけ)。発注した人はログイン中のアカウント (認証を無効にしているときは null)
    private static async ValueTask<IResult> HandleOrderAsync(
        PurchaseOrderService service,
        ClaimsPrincipal user,
        Guid id,
        CancellationToken cancellationToken)
    {
        return ToResult(await service.OrderAsync(id, AuthClaims.AccountOf(user)?.Name, cancellationToken));
    }

    // キャンセル (下書きか発注済み)。発注済みは入荷予定もキャンセルする
    private static async ValueTask<IResult> HandleCancelAsync(
        PurchaseOrderService service,
        Guid id,
        CancellationToken cancellationToken)
    {
        return ToResult(await service.CancelAsync(id, cancellationToken));
    }
}
