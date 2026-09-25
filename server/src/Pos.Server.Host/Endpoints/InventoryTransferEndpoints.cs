namespace Pos.Server.Host.Endpoints;

using Pos.Contract.InventoryTransfers;
using Pos.Server.Host.Helpers;
using Pos.Server.Models.Entity;
using Pos.Server.Models.Parameters;
using Pos.Server.Models.Views;
using Pos.Server.Services;

using Smart.Mapper;

// 店舗間移動 (依頼・出荷・受領)
public static partial class InventoryTransferEndpoints
{
    //--------------------------------------------------------------------------------
    // Mapping
    //--------------------------------------------------------------------------------

    public static void MapInventoryTransferEndpoints(this WebApplication app)
    {
        var group = app.MapApiGroup(ApiRoutes.InventoryTransfers);
        group.MapGet("/", HandleListAsync);
        group.MapGet("/{id:guid}", HandleGetAsync);
        group.MapPost("/", HandleCreateAsync).RequireAuthorization(Policies.Admin);
        group.MapPost("/{id:guid}/ship", HandleShipAsync).RequireAuthorization(Policies.Admin);
        group.MapPost("/{id:guid}/cancel", HandleCancelAsync).RequireAuthorization(Policies.Admin);
        group.MapPost("/{id:guid}/receive", HandleReceiveAsync);
    }

    //--------------------------------------------------------------------------------
    // Mapper
    //--------------------------------------------------------------------------------

    [Mapper]
    private static partial InventoryTransferLineEntity ToEntity(InventoryTransferCreateRequestLine request);

    [Mapper]
    private static partial InventoryTransferResponseItem ToResponseCore(InventoryTransferEntity entity);

    [Mapper]
    private static partial InventoryTransferResponseLine ToResponse(InventoryTransferLineEntity entity);

    private static InventoryTransferResponseItem ToResponse(InventoryTransferDetailView detail)
    {
        var response = ToResponseCore(detail.Transfer);
        response.FromStoreName = detail.FromStoreName;
        response.ToStoreName = detail.ToStoreName;
        response.Lines = detail.Lines.Select(ToResponse).ToList();
        return response;
    }

    private static IResult ToResult(InventoryTransferResult result) =>
        result.Status switch
        {
            InventoryTransferResultStatus.Success => TypedResults.Ok(ToResponse(result.Detail!)),
            InventoryTransferResultStatus.NotFound => ApiProblems.NotFound(),
            _ => ApiProblems.FromViolation(result.Violation!)
        };

    //--------------------------------------------------------------------------------
    // Handler
    //--------------------------------------------------------------------------------

    // storeId は出荷店か入荷店のどちらか。open = true は未受領 (依頼・出荷済み) だけ
    private static async ValueTask<IResult> HandleListAsync(
        InventoryTransferService service,
        Guid? storeId,
        Guid? fromStoreId,
        Guid? toStoreId,
        InventoryTransferStatus? status,
        string? sort,
        CancellationToken cancellationToken,
        bool open = false,
        bool desc = true,
        [Range(0, Int32.MaxValue)] int page = 0,
        [Range(1, ApiDefaults.MaxPageSize)] int size = ApiDefaults.PageSize)
    {
        var parameter = new InventoryTransferQueryParameter
        {
            StoreId = storeId,
            FromStoreId = fromStoreId,
            ToStoreId = toStoreId,
            Status = status,
            OpenOnly = open,
            Sort = EnumHelper.Parse(sort, InventoryTransferSort.CreatedAt),
            Desc = desc,
            Page = page,
            Size = size
        };
        var result = await service.QueryPageAsync(parameter, cancellationToken);
        return TypedResults.Ok(new InventoryTransferResponse { Total = result.Total, Page = result.Page, Size = result.Size, Items = result.Items.Select(ToResponse).ToList() });
    }

    private static async ValueTask<IResult> HandleGetAsync(
        InventoryTransferService service,
        Guid id,
        CancellationToken cancellationToken)
    {
        var detail = await service.QueryDetailAsync(id, cancellationToken);
        return detail is null ? ApiProblems.NotFound() : TypedResults.Ok(ToResponse(detail));
    }

    // 出荷店と入荷店が同じなら 400 (要求の検証)
    private static async ValueTask<IResult> HandleCreateAsync(
        InventoryTransferService service,
        InventoryTransferCreateRequest request,
        CancellationToken cancellationToken)
    {
        var result = await service.CreateAsync(request.FromStoreId, request.ToStoreId, request.Note, request.Lines.Select(ToEntity).ToList(), cancellationToken);
        return result.Status == InventoryTransferResultStatus.Success
            ? TypedResults.Created($"{ApiRoutes.InventoryTransfers}/{result.Detail!.Transfer.Id}", ToResponse(result.Detail))
            : ToResult(result);
    }

    // 出荷 (依頼のときだけ)。依頼の数で出荷店の在庫を減らす
    private static async ValueTask<IResult> HandleShipAsync(
        InventoryTransferService service,
        Guid id,
        InventoryTransferShipRequest request,
        CancellationToken cancellationToken)
    {
        return ToResult(await service.ShipAsync(id, request.StaffId, request.ShippedAt, cancellationToken));
    }

    // 受領 (出荷済みのときだけ)。明細を省略すると出荷した数で受け取る
    // 端末の受領は入荷店が自店であること
    private static async ValueTask<IResult> HandleReceiveAsync(
        TerminalAccess access,
        InventoryTransferService service,
        ClaimsPrincipal user,
        Guid id,
        InventoryTransferReceiveRequest request,
        CancellationToken cancellationToken)
    {
        if ((await service.QueryDetailAsync(id, cancellationToken) is { } detail) && !access.CanAccess(user, detail.Transfer.ToStoreId))
        {
            return ApiProblems.TerminalMismatch();
        }

        var quantities = request.Lines.ToDictionary(static x => x.LineId, static x => x.Quantity);
        return ToResult(await service.ReceiveAsync(id, request.StaffId, request.ReceivedAt, quantities, cancellationToken));
    }

    private static async ValueTask<IResult> HandleCancelAsync(
        InventoryTransferService service,
        Guid id,
        CancellationToken cancellationToken)
    {
        return ToResult(await service.CancelAsync(id, cancellationToken));
    }
}
