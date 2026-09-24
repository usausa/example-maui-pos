namespace Pos.Server.Host.Endpoints;

using Pos.Contract.Orders;
using Pos.Server.Host.Helpers;
using Pos.Server.Models.Entity;
using Pos.Server.Models.Parameters;
using Pos.Server.Models.Views;
using Pos.Server.Services;

using Smart.Mapper;

// 受注 (取り寄せ・取り置き)
public static partial class OrderEndpoints
{
    //--------------------------------------------------------------------------------
    // Mapping
    //--------------------------------------------------------------------------------

    public static void MapOrderEndpoints(this WebApplication app)
    {
        var group = app.MapApiGroup(ApiRoutes.Orders);
        // 認証の導入時: 端末の要求は storeId / terminalId (入荷・キャンセルは受注の店舗) が端末トークンのクレームと一致することを確かめる
        group.MapPost("/", HandleCreateAsync);
        group.MapGet("/", HandleListAsync);
        group.MapGet("/{id:guid}", HandleGetAsync);
        // 認証の導入時: 変更は管理画面だけ (Admin)
        group.MapPut("/{id:guid}", HandleUpdateAsync);
        group.MapPost("/{id:guid}/arrive", HandleArriveAsync);
        group.MapPost("/{id:guid}/cancel", HandleCancelAsync);
    }

    //--------------------------------------------------------------------------------
    // Mapper
    //--------------------------------------------------------------------------------

    [Mapper]
    private static partial OrderEntity ToEntity(OrderCreateRequest request);

    [Mapper]
    private static partial OrderLineEntity ToEntity(OrderCreateRequestLine request);

    [Mapper]
    private static partial OrderLineEntity ToEntity(OrderUpdateRequestLine request);

    [Mapper]
    [MapUsing(nameof(OrderUpdateParameter.Lines), nameof(ToLines))]
    private static partial OrderUpdateParameter ToParameter(OrderUpdateRequest request);

    private static List<OrderLineEntity> ToLines(OrderUpdateRequest request) => request.Lines.Select(ToEntity).ToList();

    private static OrderDetailView ToDetail(OrderCreateRequest request) =>
        new()
        {
            Order = ToEntity(request),
            Lines = request.Lines.Select(ToEntity).ToList()
        };

    [Mapper]
    private static partial OrderResponseItem ToResponseCore(OrderEntity entity);

    [Mapper]
    private static partial OrderResponseLine ToResponse(OrderLineEntity entity);

    private static OrderResponseItem ToResponse(OrderDetailView detail)
    {
        var response = ToResponseCore(detail.Order);
        response.Lines = detail.Lines.Select(ToResponse).ToList();
        return response;
    }

    // 登録以外の結果 (変更・入荷・キャンセル)
    private static IResult ToResult(OrderResult result) =>
        result.Status switch
        {
            OrderResultStatus.Success or OrderResultStatus.Existing => TypedResults.Ok(ToResponse(result.Detail!)),
            OrderResultStatus.NotFound => ApiProblems.NotFound(),
            OrderResultStatus.VersionMismatch => ApiProblems.VersionMismatch(),
            OrderResultStatus.DuplicateMismatch => ApiProblems.DuplicateIdMismatch(),
            _ => ApiProblems.FromViolation(result.Violation!)
        };

    //--------------------------------------------------------------------------------
    // Handler
    //--------------------------------------------------------------------------------

    // 登録。同じ id は 200 で既存を返し、内容が違えば 409
    private static async ValueTask<IResult> HandleCreateAsync(
        OrderService service,
        OrderCreateRequest request,
        CancellationToken cancellationToken)
    {
        var result = await service.CreateAsync(ToDetail(request), cancellationToken);
        return result.Status == OrderResultStatus.Success
            ? TypedResults.Created($"{ApiRoutes.Orders}/{request.Id}", ToResponse(result.Detail!))
            : ToResult(result);
    }

    // open = true は未完了 (入荷待ち・引き渡し待ち) だけ。keyword は受注番号・宛名・電話の部分一致、from / to は受注日
    private static async ValueTask<IResult> HandleListAsync(
        OrderService service,
        Guid? storeId,
        OrderStatus? status,
        OrderType? type,
        Guid? customerId,
        string? keyword,
        DateOnly? from,
        DateOnly? to,
        string? sort,
        CancellationToken cancellationToken,
        bool open = false,
        bool desc = true,
        [Range(0, Int32.MaxValue)] int page = 0,
        [Range(1, ApiDefaults.MaxPageSize)] int size = ApiDefaults.PageSize)
    {
        var parameter = new OrderQueryParameter
        {
            StoreId = storeId,
            Status = status,
            OpenOnly = open,
            Type = type,
            CustomerId = customerId,
            Keyword = keyword,
            From = from,
            To = to,
            Sort = EnumHelper.Parse(sort, OrderSort.OrderedAt),
            Desc = desc,
            Page = page,
            Size = size
        };
        var result = await service.QueryPageAsync(parameter, cancellationToken);
        return TypedResults.Ok(new OrderResponse { Total = result.Total, Page = result.Page, Size = result.Size, Items = result.Items.Select(ToResponse).ToList() });
    }

    private static async ValueTask<IResult> HandleGetAsync(
        OrderService service,
        Guid id,
        CancellationToken cancellationToken)
    {
        var detail = await service.QueryDetailAsync(id, cancellationToken);
        return detail is null ? ApiProblems.NotFound() : TypedResults.Ok(ToResponse(detail));
    }

    // 変更 (未完了のときだけ。完了・キャンセル済みは 422)
    private static async ValueTask<IResult> HandleUpdateAsync(
        OrderService service,
        Guid id,
        OrderUpdateRequest request,
        CancellationToken cancellationToken)
    {
        return ToResult(await service.UpdateAsync(id, ToParameter(request), cancellationToken));
    }

    // 入荷 (入荷待ちのときだけ)
    private static async ValueTask<IResult> HandleArriveAsync(
        OrderService service,
        Guid id,
        CancellationToken cancellationToken)
    {
        return ToResult(await service.ArriveAsync(id, cancellationToken));
    }

    // キャンセル (未完了のときだけ)
    private static async ValueTask<IResult> HandleCancelAsync(
        OrderService service,
        Guid id,
        OrderCancelRequest request,
        CancellationToken cancellationToken)
    {
        return ToResult(await service.CancelAsync(id, request.Reason, cancellationToken));
    }
}
