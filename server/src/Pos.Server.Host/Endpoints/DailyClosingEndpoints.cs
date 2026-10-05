namespace Pos.Server.Host.Endpoints;

using Pos.Contract.DailyClosings;
using Pos.Server.Host.Helpers;
using Pos.Server.Models.Entity;
using Pos.Server.Models.Parameters;
using Pos.Server.Models.Views;
using Pos.Server.Services;

using Smart.Mapper;

// 日次締め (店舗 × 営業日)
public static partial class DailyClosingEndpoints
{
    //--------------------------------------------------------------------------------
    // Mapping
    //--------------------------------------------------------------------------------

    public static void MapDailyClosingEndpoints(this WebApplication app)
    {
        // 日次締めは管理画面だけで行い、締めの解除は管理者に限る
        var group = app.MapApiGroup(ApiRoutes.DailyClosings).RequireAuthorization(Policies.Admin);
        group.MapPost("/", HandleCloseAsync)
            .WithName("DailyClosingClose")
            .Produces<DailyClosingSummaryResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);
        group.MapGet("/", HandleListAsync)
            .WithName("DailyClosingList")
            .Produces<DailyClosingListResponse>()
            .ProducesValidationProblem();
        group.MapGet("/preview", HandlePreviewAsync)
            .WithName("DailyClosingPreview")
            .Produces<DailyClosingSummaryResponse>();
        group.MapGet("/{id:guid}", HandleGetAsync)
            .WithName("DailyClosingGet")
            .Produces<DailyClosingSummaryResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound);
        group.MapDelete("/{id:guid}", HandleReopenAsync)
            .RequireAuthorization(Policies.Administrator)
            .WithName("DailyClosingReopen")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);
    }

    //--------------------------------------------------------------------------------
    // Mapper
    //--------------------------------------------------------------------------------

    [Mapper]
    private static partial DailyClosingListResponseItem ToListResponseItem(DailyClosingDayView day);

    [Mapper]
    private static partial DailyClosingSummaryResponsePaymentMethod ToSummaryResponsePaymentMethod(PaymentMethodTotalView total);

    [Mapper]
    private static partial DailyClosingSummaryResponseTaxRate ToSummaryResponseTaxRate(TaxRateTotalView total);

    [Mapper]
    private static partial DailyClosingSummaryResponseShift ToSummaryResponseShift(ShiftEntity shift);

    private static DailyClosingSummaryResponse ToSummaryResponse(DailyClosingSummaryView summary) =>
        new()
        {
            DailyClosing = ToListResponseItem(summary.Day),
            ByPaymentMethod = summary.ByPaymentMethod.Select(ToSummaryResponsePaymentMethod).ToList(),
            ByTaxRate = summary.ByTaxRate.Select(ToSummaryResponseTaxRate).ToList(),
            Shifts = summary.Shifts.Select(ToSummaryResponseShift).ToList()
        };

    //--------------------------------------------------------------------------------
    // Handler
    //--------------------------------------------------------------------------------

    // 締め。シフトがなければ 422、未精算のシフトがあれば 422、締め済みは 409
    private static async ValueTask<IResult> HandleCloseAsync(
        DailyClosingService service,
        ClaimsPrincipal user,
        DailyClosingCloseRequest request,
        CancellationToken cancellationToken)
    {
        // 締めた人はログイン中のアカウント (認証を無効にしてログインしていなければ記録しない)
        var result = await service.CloseAsync(request.StoreId, request.BusinessDate, AuthClaims.AccountOf(user)?.Name, cancellationToken);
        return result.Status switch
        {
            DailyClosingResultStatus.Success => TypedResults.Created($"{ApiRoutes.DailyClosings}/{result.Summary!.Day.Id}", ToSummaryResponse(result.Summary)),
            DailyClosingResultStatus.NoShift => ApiProblems.Unprocessable(ErrorCode.ShiftNotFound, "この営業日のシフトがありません"),
            DailyClosingResultStatus.ShiftStillOpen => ApiProblems.Unprocessable(ErrorCode.ShiftStillOpen, "未精算のシフトがあります", $"未精算 {result.Summary!.Day.OpenShiftCount} 件"),
            _ => ApiProblems.Problem(StatusCodes.Status409Conflict, ErrorCode.AlreadyClosed, "既に締め済みです")
        };
    }

    // 店舗 × 営業日の一覧 (シフト・取引・締めのある日)。営業日の降順、同じ日は店舗コード順
    private static async ValueTask<IResult> HandleListAsync(
        DailyClosingService service,
        Guid? storeId,
        DailyClosingStatus? status,
        DateOnly? from,
        DateOnly? to,
        string? sort,
        CancellationToken cancellationToken,
        bool desc = true,
        [Range(0, Int32.MaxValue)] int page = 0,
        [Range(1, ApiDefaults.MaxPageSize)] int size = ApiDefaults.PageSize)
    {
        var parameter = new DailyClosingQueryParameter { StoreId = storeId, Status = status, From = from, To = to, Sort = EnumHelper.Parse(sort, DailyClosingSort.BusinessDate), Desc = desc, Page = page, Size = size };
        var result = await service.QueryDayPageAsync(parameter, cancellationToken);
        return TypedResults.Ok(new DailyClosingListResponse { Total = result.Total, Page = result.Page, Size = result.Size, Items = result.Items.Select(ToListResponseItem).ToList() });
    }

    // 店舗 × 営業日の内容。未締めは取引からの集計 (締める前の確認)、締め済みは締めた内容
    private static async ValueTask<IResult> HandlePreviewAsync(
        DailyClosingService service,
        Guid storeId,
        DateOnly businessDate,
        CancellationToken cancellationToken)
    {
        var summary = await service.QuerySummaryAsync(storeId, businessDate, cancellationToken);
        return TypedResults.Ok(ToSummaryResponse(summary));
    }

    private static async ValueTask<IResult> HandleGetAsync(
        DailyClosingService service,
        Guid id,
        CancellationToken cancellationToken)
    {
        var summary = await service.QuerySummaryAsync(id, cancellationToken);
        return summary is null ? ApiProblems.NotFound() : TypedResults.Ok(ToSummaryResponse(summary));
    }

    // 締め解除 (日計と内訳を消して未締めに戻す)
    private static async ValueTask<IResult> HandleReopenAsync(
        DailyClosingService service,
        Guid id,
        CancellationToken cancellationToken)
    {
        return await service.ReopenAsync(id, cancellationToken) ? TypedResults.NoContent() : ApiProblems.NotFound();
    }
}
