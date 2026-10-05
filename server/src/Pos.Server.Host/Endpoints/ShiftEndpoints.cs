namespace Pos.Server.Host.Endpoints;

using Pos.Contract.Shifts;
using Pos.Server.Host.Helpers;
using Pos.Server.Host.Reports;
using Pos.Server.Models.Entity;
using Pos.Server.Models.Parameters;
using Pos.Server.Models.Views;
using Pos.Server.Services;

using Smart.Mapper;

// レジ開閉・現金管理
public static partial class ShiftEndpoints
{
    //--------------------------------------------------------------------------------
    // Mapping
    //--------------------------------------------------------------------------------

    public static void MapShiftEndpoints(this WebApplication app)
    {
        var group = app.MapApiGroup(ApiRoutes.Shifts);
        group.MapPost("/", HandleOpenAsync)
            .WithName("ShiftOpen")
            .Produces<ShiftListResponseItem>()
            .Produces<ShiftListResponseItem>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status409Conflict);
        group.MapGet("/current", HandleCurrentAsync)
            .WithName("ShiftCurrent")
            .Produces<ShiftListResponseItem>()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);
        group.MapGet("/", HandleListAsync)
            .RequireAuthorization(Policies.Admin)
            .WithName("ShiftList")
            .Produces<ShiftListResponse>()
            .ProducesValidationProblem()
            .Produces(StatusCodes.Status403Forbidden);
        group.MapGet("/{id:guid}", HandleGetAsync)
            .WithName("ShiftGet")
            .Produces<ShiftListResponseItem>()
            .ProducesProblem(StatusCodes.Status404NotFound);
        group.MapPost("/{id:guid}/cash-events", HandleCashEventAsync)
            .WithName("ShiftCashEvent")
            .Produces<ShiftCashEventListResponseItem>()
            .Produces<ShiftCashEventListResponseItem>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);
        group.MapGet("/{id:guid}/cash-events", HandleCashEventListAsync)
            .WithName("ShiftCashEventList")
            .Produces<ShiftCashEventListResponse>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound);
        group.MapPost("/{id:guid}/close", HandleCloseAsync)
            .WithName("ShiftClose")
            .Produces<ShiftListResponseItem>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);
        group.MapGet("/{id:guid}/summary", HandleSummaryAsync)
            .WithName("ShiftSummary")
            .Produces<ShiftSummaryResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound);
        group.MapGet("/{id:guid}/summary/pdf", HandleSummaryPdfAsync)
            .RequireAuthorization(Policies.Admin)
            .WithName("ShiftSummaryPdf")
            .Produces<Stream>(StatusCodes.Status200OK, "application/pdf")
            .Produces(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);
    }

    //--------------------------------------------------------------------------------
    // Mapper
    //--------------------------------------------------------------------------------

    [Mapper]
    private static partial ShiftEntity ToEntity(ShiftOpenRequest request);

    [Mapper]
    private static partial CashEventEntity ToEntity(ShiftCashEventRequest request);

    [Mapper]
    [MapUsing(nameof(ShiftCloseParameter.Denominations), nameof(ToDenominations))]
    private static partial ShiftCloseParameter ToParameter(ShiftCloseRequest request);

    [Mapper]
    private static partial ShiftDenominationEntity ToEntity(ShiftCloseRequestDenomination request);

    private static List<ShiftDenominationEntity> ToDenominations(ShiftCloseRequest request) => request.Denominations.Select(ToEntity).ToList();

    [Mapper]
    private static partial ShiftListResponseItem ToResponseCore(ShiftEntity entity);

    [Mapper]
    private static partial ShiftListResponseDenomination ToResponse(ShiftDenominationEntity entity);

    [Mapper]
    private static partial ShiftListResponseTotals ToResponse(ShiftTotalsView totals);

    [Mapper]
    private static partial ShiftCashEventListResponseItem ToResponse(CashEventEntity entity);

    [Mapper]
    private static partial ShiftSummaryResponsePaymentMethod ToResponse(PaymentMethodTotalView total);

    [Mapper]
    private static partial ShiftSummaryResponseTaxRate ToResponse(TaxRateTotalView total);

    [Mapper]
    private static partial ShiftSummaryResponseCategory ToResponse(CategoryTotalView total);

    private static ShiftListResponseItem ToResponse(ShiftDetailView detail)
    {
        var response = ToResponseCore(detail.Shift);
        response.Totals = ToResponse(detail.Totals);
        response.Denominations = detail.Denominations.Select(ToResponse).ToList();
        response.ExpectedCash = detail.ExpectedCash;
        return response;
    }

    private static ShiftSummaryResponse ToResponse(ShiftSummaryView summary)
    {
        var shift = ToResponse(summary.Shift);
        return new ShiftSummaryResponse
        {
            Shift = shift,
            ByPaymentMethod = summary.ByPaymentMethod.Select(ToResponse).ToList(),
            ByTaxRate = summary.ByTaxRate.Select(ToResponse).ToList(),
            ByCategory = summary.ByCategory.Select(ToResponse).ToList(),
            Points = new ShiftSummaryResponsePoints { Earned = summary.Points.Earned, Redeemed = summary.Points.Redeemed },
            Cash = new ShiftSummaryResponseCash
            {
                OpeningCash = shift.OpeningCash,
                CashSales = shift.Totals.CashSales,
                CashReturns = shift.Totals.CashReturns,
                PaidIn = shift.Totals.PaidIn,
                PaidOut = shift.Totals.PaidOut,
                DepositCashIn = shift.Totals.DepositCashIn,
                DepositCashOut = shift.Totals.DepositCashOut,
                ExpectedCash = shift.ExpectedCash,
                ActualCash = shift.ActualCash,
                Difference = shift.Difference
            }
        };
    }

    //--------------------------------------------------------------------------------
    // Handler
    //--------------------------------------------------------------------------------

    // 開設。同じ id は 200 で既存を返し、端末に Open のシフトがあれば 409
    private static async ValueTask<IResult> HandleOpenAsync(
        TerminalAccess access,
        ShiftService service,
        ClaimsPrincipal user,
        ShiftOpenRequest request,
        CancellationToken cancellationToken)
    {
        if (!access.CanAccess(user, request.StoreId, request.TerminalId))
        {
            return ApiProblems.TerminalMismatch();
        }

        var entity = ToEntity(request);
        var result = await service.OpenAsync(entity, cancellationToken);
        return result.Status switch
        {
            ShiftResultStatus.Success => TypedResults.Created($"{ApiRoutes.Shifts}/{entity.Id}", ToResponse(result.Detail!)),
            ShiftResultStatus.Existing => TypedResults.Ok(ToResponse(result.Detail!)),
            ShiftResultStatus.DuplicateMismatch => ApiProblems.DuplicateIdMismatch(),
            _ => ApiProblems.Problem(StatusCodes.Status409Conflict, ErrorCode.TerminalHasOpenShift, "この端末には開設中のシフトがあります")
        };
    }

    private static async ValueTask<IResult> HandleCurrentAsync(
        TerminalAccess access,
        ShiftService service,
        ClaimsPrincipal user,
        Guid terminalId,
        CancellationToken cancellationToken)
    {
        if (!access.CanAccessTerminal(user, terminalId))
        {
            return ApiProblems.TerminalMismatch();
        }

        var detail = await service.QueryCurrentAsync(terminalId, cancellationToken);
        return detail is null ? ApiProblems.NotFound("開設中のシフトはありません") : TypedResults.Ok(ToResponse(detail));
    }

    private static async ValueTask<IResult> HandleListAsync(
        ShiftService service,
        Guid? storeId,
        Guid? terminalId,
        ShiftStatus? status,
        DateOnly? from,
        DateOnly? to,
        string? sort,
        CancellationToken cancellationToken,
        bool desc = true,
        [Range(0, Int32.MaxValue)] int page = 0,
        [Range(1, ApiDefaults.MaxPageSize)] int size = ApiDefaults.PageSize)
    {
        var parameter = new ShiftQueryParameter { StoreId = storeId, TerminalId = terminalId, Status = status, From = from, To = to, Sort = EnumHelper.Parse(sort, ShiftSort.OpenedAt), Desc = desc, Page = page, Size = size };
        var result = await service.QueryDetailPageAsync(parameter, cancellationToken);
        return TypedResults.Ok(new ShiftListResponse { Total = result.Total, Page = result.Page, Size = result.Size, Items = result.Items.Select(ToResponse).ToList() });
    }

    private static async ValueTask<IResult> HandleGetAsync(
        ShiftService service,
        Guid id,
        CancellationToken cancellationToken)
    {
        var detail = await service.QueryDetailAsync(id, cancellationToken);
        return detail is null ? ApiProblems.NotFound() : TypedResults.Ok(ToResponse(detail));
    }

    // 入出金 (Open のみ)。同じ id は 200 で既存を返す
    private static async ValueTask<IResult> HandleCashEventAsync(
        TerminalAccess access,
        ShiftService service,
        ClaimsPrincipal user,
        Guid id,
        ShiftCashEventRequest request,
        CancellationToken cancellationToken)
    {
        if ((await service.QueryAsync(id, cancellationToken) is { } shift) && !access.CanAccessTerminal(user, shift.TerminalId))
        {
            return ApiProblems.TerminalMismatch();
        }

        var entity = ToEntity(request);
        entity.ShiftId = id;
        var result = await service.AddCashEventAsync(entity, cancellationToken);
        return result.Status switch
        {
            CashEventResultStatus.Success => TypedResults.Created($"{ApiRoutes.Shifts}/{id}/cash-events/{entity.Id}", ToResponse(result.Entity!)),
            CashEventResultStatus.Existing => TypedResults.Ok(ToResponse(result.Entity!)),
            CashEventResultStatus.DuplicateMismatch => ApiProblems.DuplicateIdMismatch(),
            CashEventResultStatus.ShiftNotFound => ApiProblems.Unprocessable(ErrorCode.ShiftNotFound, "シフトが見つかりません"),
            _ => ApiProblems.Unprocessable(ErrorCode.ShiftClosed, "精算済みのシフトには登録できません")
        };
    }

    private static async ValueTask<IResult> HandleCashEventListAsync(
        ShiftService service,
        Guid id,
        CancellationToken cancellationToken,
        [Range(0, Int32.MaxValue)] int page = 0,
        [Range(1, ApiDefaults.MaxPageSize)] int size = ApiDefaults.PageSize)
    {
        var result = await service.QueryCashEventPageAsync(id, page, size, cancellationToken);
        return result is null
            ? ApiProblems.NotFound()
            : TypedResults.Ok(new ShiftCashEventListResponse { Total = result.Total, Page = result.Page, Size = result.Size, Items = result.Items.Select(ToResponse).ToList() });
    }

    // 精算: 集計を確定して Closed にする (取引・入出金は送信済みであること)。同じ内容の再送は 200
    private static async ValueTask<IResult> HandleCloseAsync(
        TerminalAccess access,
        ShiftService service,
        ClaimsPrincipal user,
        Guid id,
        ShiftCloseRequest request,
        CancellationToken cancellationToken)
    {
        if ((await service.QueryAsync(id, cancellationToken) is { } shift) && !access.CanAccessTerminal(user, shift.TerminalId))
        {
            return ApiProblems.TerminalMismatch();
        }

        var result = await service.CloseAsync(id, ToParameter(request), cancellationToken);
        return result.Status switch
        {
            ShiftResultStatus.Success or ShiftResultStatus.Existing => TypedResults.Ok(ToResponse(result.Detail!)),
            ShiftResultStatus.NotFound => ApiProblems.NotFound(),
            _ => ApiProblems.Unprocessable(ErrorCode.ShiftClosed, "既に精算済みです")
        };
    }

    private static async ValueTask<IResult> HandleSummaryAsync(
        ShiftService service,
        Guid id,
        CancellationToken cancellationToken)
    {
        var summary = await service.QuerySummaryAsync(id, cancellationToken);
        return summary is null ? ApiProblems.NotFound() : TypedResults.Ok(ToResponse(summary));
    }

    // 精算レポート PDF
    private static async ValueTask<IResult> HandleSummaryPdfAsync(
        ShiftService service,
        ShiftReportBuilder reportBuilder,
        Guid id,
        CancellationToken cancellationToken)
    {
        var report = await service.QueryReportAsync(id, cancellationToken);
        if (report is null)
        {
            return ApiProblems.NotFound();
        }

        var bytes = reportBuilder.Build(report);
        return TypedResults.File(bytes, "application/pdf", $"shift-report-{report.Summary.Shift.Shift.BusinessDate:yyyyMMdd}-{report.TerminalNo:00}.pdf");
    }
}
