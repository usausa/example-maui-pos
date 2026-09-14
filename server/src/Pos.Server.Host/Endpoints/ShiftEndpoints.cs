namespace Pos.Server.Host.Endpoints;

using Pos.Contract.Shifts;
using Pos.Server.Host.Application.Reports;
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
        var group = app.MapGroup(ApiRoutes.Shifts);
        group.MapPost("/", HandleOpenAsync);
        group.MapGet("/current", HandleCurrentAsync);
        group.MapGet("/", HandleListAsync);
        group.MapGet("/{id:guid}", HandleGetAsync);
        group.MapPost("/{id:guid}/cash-events", HandleCashEventAsync);
        group.MapGet("/{id:guid}/cash-events", HandleCashEventListAsync);
        group.MapPost("/{id:guid}/close", HandleCloseAsync);
        group.MapGet("/{id:guid}/summary", HandleSummaryAsync);
        group.MapGet("/{id:guid}/summary/pdf", HandleSummaryPdfAsync);
    }

    //--------------------------------------------------------------------------------
    // Mapper
    //--------------------------------------------------------------------------------

    [Mapper]
    private static partial ShiftEntity ToEntity(ShiftOpenRequest request);

    [Mapper]
    private static partial CashEventEntity ToEntity(CashEventRequest request);

    [Mapper]
    [MapUsing(nameof(ShiftCloseParameter.Denominations), nameof(ToDenominations))]
    private static partial ShiftCloseParameter ToParameter(ShiftCloseRequest request);

    [Mapper]
    private static partial ShiftDenominationEntity ToEntity(ShiftCloseRequestDenomination request);

    private static List<ShiftDenominationEntity> ToDenominations(ShiftCloseRequest request) => request.Denominations.Select(ToEntity).ToList();

    [Mapper]
    private static partial ShiftResponseItem ToResponseCore(ShiftEntity entity);

    [Mapper]
    private static partial ShiftResponseItemDenomination ToResponse(ShiftDenominationEntity entity);

    [Mapper]
    private static partial ShiftResponseItemTotals ToResponse(ShiftTotals totals);

    [Mapper]
    private static partial CashEventResponseItem ToResponse(CashEventEntity entity);

    [Mapper]
    private static partial ShiftSummaryResponsePaymentMethod ToResponse(PaymentMethodTotal total);

    [Mapper]
    private static partial ShiftSummaryResponseTaxRate ToResponse(TaxRateTotal total);

    [Mapper]
    private static partial ShiftSummaryResponseCategory ToResponse(CategoryTotal total);

    private static ShiftResponseItem ToResponse(ShiftDetail detail)
    {
        var response = ToResponseCore(detail.Shift);
        response.Totals = ToResponse(detail.Totals);
        response.Denominations = detail.Denominations.Select(ToResponse).ToList();
        response.ExpectedCash = detail.ExpectedCash;
        return response;
    }

    private static ShiftSummaryResponse ToResponse(ShiftSummary summary)
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
        ShiftService service,
        ShiftOpenRequest request,
        CancellationToken cancellationToken)
    {
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
        ShiftService service,
        Guid terminalId,
        CancellationToken cancellationToken)
    {
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
        var parameter = new ShiftQueryParameter { StoreId = storeId, TerminalId = terminalId, Status = status, From = from, To = to, Sort = sort, Desc = desc, Page = page, Size = size };
        var result = await service.QueryDetailPageAsync(parameter, cancellationToken);
        return TypedResults.Ok(new ShiftResponse { Total = result.Total, Page = result.Page, Size = result.Size, Items = result.Items.Select(ToResponse).ToList() });
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
        ShiftService service,
        Guid id,
        CashEventRequest request,
        CancellationToken cancellationToken)
    {
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
            : TypedResults.Ok(new CashEventResponse { Total = result.Total, Page = result.Page, Size = result.Size, Items = result.Items.Select(ToResponse).ToList() });
    }

    // 精算: 集計を確定して Closed にする (取引・入出金は送信済みであること)。同じ内容の再送は 200
    private static async ValueTask<IResult> HandleCloseAsync(
        ShiftService service,
        Guid id,
        ShiftCloseRequest request,
        CancellationToken cancellationToken)
    {
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
