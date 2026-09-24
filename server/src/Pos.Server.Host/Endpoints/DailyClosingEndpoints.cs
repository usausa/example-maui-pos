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
        // 認証の導入時: 日次締めは管理画面だけ (Admin)
        var group = app.MapApiGroup(ApiRoutes.DailyClosings);
        group.MapPost("/", HandleCloseAsync);
        group.MapGet("/", HandleListAsync);
        group.MapGet("/preview", HandlePreviewAsync);
        group.MapGet("/{id:guid}", HandleGetAsync);
        // 認証の導入時: 締め解除は Administrator に限る
        group.MapDelete("/{id:guid}", HandleReopenAsync);
    }

    //--------------------------------------------------------------------------------
    // Mapper
    //--------------------------------------------------------------------------------

    [Mapper]
    private static partial DailyClosingResponseItem ToResponse(DailyClosingDayView day);

    [Mapper]
    private static partial DailyClosingSummaryResponsePaymentMethod ToResponse(PaymentMethodTotalView total);

    [Mapper]
    private static partial DailyClosingSummaryResponseTaxRate ToResponse(TaxRateTotalView total);

    [Mapper]
    private static partial DailyClosingSummaryResponseShift ToResponse(ShiftEntity shift);

    private static DailyClosingSummaryResponse ToResponse(DailyClosingSummaryView summary) =>
        new()
        {
            DailyClosing = ToResponse(summary.Day),
            ByPaymentMethod = summary.ByPaymentMethod.Select(ToResponse).ToList(),
            ByTaxRate = summary.ByTaxRate.Select(ToResponse).ToList(),
            Shifts = summary.Shifts.Select(ToResponse).ToList()
        };

    //--------------------------------------------------------------------------------
    // Handler
    //--------------------------------------------------------------------------------

    // 締め。シフトがなければ 422、未精算のシフトがあれば 422、締め済みは 409
    private static async ValueTask<IResult> HandleCloseAsync(
        DailyClosingService service,
        DailyClosingCreateRequest request,
        CancellationToken cancellationToken)
    {
        // 認証の導入時: 締めた人 (closedBy) にログイン中のアカウント名を渡す
        var result = await service.CloseAsync(request.StoreId, request.BusinessDate, null, cancellationToken);
        return result.Status switch
        {
            DailyClosingResultStatus.Success => TypedResults.Created($"{ApiRoutes.DailyClosings}/{result.Summary!.Day.Id}", ToResponse(result.Summary)),
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
        return TypedResults.Ok(new DailyClosingResponse { Total = result.Total, Page = result.Page, Size = result.Size, Items = result.Items.Select(ToResponse).ToList() });
    }

    // 店舗 × 営業日の内容。未締めは取引からの集計 (締める前の確認)、締め済みは締めた内容
    private static async ValueTask<IResult> HandlePreviewAsync(
        DailyClosingService service,
        Guid storeId,
        DateOnly businessDate,
        CancellationToken cancellationToken)
    {
        var summary = await service.QuerySummaryAsync(storeId, businessDate, cancellationToken);
        return TypedResults.Ok(ToResponse(summary));
    }

    private static async ValueTask<IResult> HandleGetAsync(
        DailyClosingService service,
        Guid id,
        CancellationToken cancellationToken)
    {
        var summary = await service.QuerySummaryAsync(id, cancellationToken);
        return summary is null ? ApiProblems.NotFound() : TypedResults.Ok(ToResponse(summary));
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
