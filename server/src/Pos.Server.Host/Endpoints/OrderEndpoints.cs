namespace Pos.Server.Host.Endpoints;

using Pos.Contract.Orders;
using Pos.Server.Host.Helpers;
using Pos.Server.Models.Entity;
using Pos.Server.Models.Parameters;
using Pos.Server.Models.Views;
using Pos.Server.Services;

using Smart.Mapper;

// 受注 (取り寄せ・取り置き) と前受金
public static partial class OrderEndpoints
{
    //--------------------------------------------------------------------------------
    // Mapping
    //--------------------------------------------------------------------------------

    public static void MapOrderEndpoints(this WebApplication app)
    {
        var group = app.MapApiGroup(ApiRoutes.Orders);
        group.MapPost("/", HandleCreateAsync);
        group.MapGet("/", HandleListAsync);
        group.MapGet("/{id:guid}", HandleGetAsync);
        group.MapPut("/{id:guid}", HandleUpdateAsync).RequireAuthorization(Policies.Admin);
        group.MapPost("/{id:guid}/arrive", HandleArriveAsync);
        group.MapPost("/{id:guid}/cancel", HandleCancelAsync);
        group.MapPost("/{id:guid}/deposit", HandleDepositAsync);
        group.MapPost("/{id:guid}/deposit/refund", HandleRefundDepositAsync);
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

    [Mapper]
    private static partial OrderResponseDeposit ToResponse(OrderDepositEntity entity);

    private static OrderResponseItem ToResponse(OrderDetailView detail)
    {
        var response = ToResponseCore(detail.Order);
        response.DepositAmount = detail.DepositBalance;
        response.Lines = detail.Lines.Select(ToResponse).ToList();
        response.Deposits = detail.Deposits.Select(ToResponse).ToList();
        return response;
    }

    // 受取日時を省略したら受付時刻 (default のまま渡す)
    private static OrderDepositEntity ToEntity(OrderDepositRequest request) =>
        new()
        {
            Id = request.Id,
            ShiftId = request.ShiftId,
            TerminalId = request.TerminalId,
            StaffId = request.StaffId,
            PaymentMethodId = request.PaymentMethodId,
            Amount = request.Amount,
            Reference = request.Reference,
            OccurredAt = request.OccurredAt ?? default
        };

    // 方法と金額はサーバが前受金から決める
    private static OrderDepositEntity ToEntity(OrderDepositRefundRequest request) =>
        new()
        {
            Id = request.Id,
            ShiftId = request.ShiftId,
            TerminalId = request.TerminalId,
            StaffId = request.StaffId,
            OccurredAt = request.OccurredAt ?? default
        };

    // 登録以外の結果 (変更・入荷・キャンセル・前受金)
    private static IResult ToResult(OrderResult result) =>
        result.Status switch
        {
            OrderResultStatus.Success or OrderResultStatus.Existing => TypedResults.Ok(ToResponse(result.Detail!)),
            OrderResultStatus.NotFound => ApiProblems.NotFound(),
            OrderResultStatus.VersionMismatch => ApiProblems.VersionMismatch(),
            OrderResultStatus.DuplicateMismatch => ApiProblems.DuplicateIdMismatch(),
            _ => ApiProblems.FromViolation(result.Violation!)
        };

    // 前受金は端末が id を決める登録なので、新規は 201 (Location は受注)
    private static IResult ToDepositResult(Guid id, OrderResult result) =>
        result.Status == OrderResultStatus.Success
            ? TypedResults.Created($"{ApiRoutes.Orders}/{id}", ToResponse(result.Detail!))
            : ToResult(result);

    //--------------------------------------------------------------------------------
    // Handler
    //--------------------------------------------------------------------------------

    // 登録。同じ id は 200 で既存を返し、内容が違えば 409
    private static async ValueTask<IResult> HandleCreateAsync(
        TerminalAccess access,
        OrderService service,
        ClaimsPrincipal user,
        OrderCreateRequest request,
        CancellationToken cancellationToken)
    {
        if (!access.CanAccess(user, request.StoreId, request.TerminalId))
        {
            return ApiProblems.TerminalMismatch();
        }

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
        TerminalAccess access,
        OrderService service,
        ClaimsPrincipal user,
        Guid id,
        CancellationToken cancellationToken)
    {
        if ((await service.QueryDetailAsync(id, cancellationToken) is { } detail) && !access.CanAccess(user, detail.Order.StoreId))
        {
            return ApiProblems.TerminalMismatch();
        }

        return ToResult(await service.ArriveAsync(id, cancellationToken));
    }

    // キャンセル (未完了のときだけ)
    private static async ValueTask<IResult> HandleCancelAsync(
        TerminalAccess access,
        OrderService service,
        ClaimsPrincipal user,
        Guid id,
        OrderCancelRequest request,
        CancellationToken cancellationToken)
    {
        if ((await service.QueryDetailAsync(id, cancellationToken) is { } detail) && !access.CanAccess(user, detail.Order.StoreId))
        {
            return ApiProblems.TerminalMismatch();
        }

        return ToResult(await service.CancelAsync(id, request.Reason, cancellationToken));
    }

    // 前受金の受取 (端末のシフト)。新規は 201 で受注を返し、同じ id は 200 で既存を、内容が違えば 409
    private static async ValueTask<IResult> HandleDepositAsync(
        TerminalAccess access,
        OrderService service,
        ClaimsPrincipal user,
        Guid id,
        OrderDepositRequest request,
        CancellationToken cancellationToken)
    {
        if ((await service.QueryDetailAsync(id, cancellationToken) is { } detail) && !access.CanAccess(user, detail.Order.StoreId, request.TerminalId))
        {
            return ApiProblems.TerminalMismatch();
        }

        return ToDepositResult(id, await service.DepositAsync(id, ToEntity(request), cancellationToken));
    }

    // 前受金の返金 (全額を受け取った方法で)。新規は 201、同じ id は 200 で既存を返す
    private static async ValueTask<IResult> HandleRefundDepositAsync(
        TerminalAccess access,
        OrderService service,
        ClaimsPrincipal user,
        Guid id,
        OrderDepositRefundRequest request,
        CancellationToken cancellationToken)
    {
        if ((await service.QueryDetailAsync(id, cancellationToken) is { } detail) && !access.CanAccess(user, detail.Order.StoreId, request.TerminalId))
        {
            return ApiProblems.TerminalMismatch();
        }

        return ToDepositResult(id, await service.RefundDepositAsync(id, ToEntity(request), cancellationToken));
    }
}
