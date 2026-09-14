namespace Pos.Server.Host.Endpoints;

using Pos.Contract.Transactions;
using Pos.Domain.Logic;
using Pos.Server.Models.Entity;
using Pos.Server.Models.Parameters;
using Pos.Server.Models.Views;
using Pos.Server.Services;

using Smart.Mapper;

// 取引の登録・計算・照会・取消
public static partial class TransactionEndpoints
{
    //--------------------------------------------------------------------------------
    // Mapping
    //--------------------------------------------------------------------------------

    public static void MapTransactionEndpoints(this WebApplication app)
    {
        var group = app.MapGroup(ApiRoutes.Transactions);
        group.MapPost("/", HandleCreateAsync);
        group.MapPost("/calculate", HandleCalculateAsync);
        group.MapGet("/", HandleListAsync);
        group.MapGet("/lookup", HandleLookupAsync);
        group.MapGet("/{id:guid}", HandleGetAsync);
        group.MapPost("/{id:guid}/void", HandleVoidAsync);
    }

    //--------------------------------------------------------------------------------
    // Mapper: Request -> Entity
    //--------------------------------------------------------------------------------

    [Mapper]
    private static partial TransactionEntity ToEntity(TransactionRequest request);

    [Mapper]
    private static partial TransactionLineEntity ToEntity(TransactionRequestLine request);

    [Mapper]
    private static partial TransactionDiscountEntity ToEntity(TransactionRequestDiscount request);

    [Mapper]
    private static partial TransactionTaxSummaryEntity ToEntity(TransactionRequestTaxSummary request);

    [Mapper]
    private static partial TransactionPaymentEntity ToEntity(TransactionRequestPayment request);

    [Mapper]
    private static partial TransactionDeliveryEntity ToEntity(TransactionRequestDelivery request);

    // 取引一式 (シリアルは明細から起こす)
    private static TransactionDetail ToDetail(TransactionRequest request)
    {
        var entity = ToEntity(request);
        if (request.Void is not null)
        {
            entity.VoidedAt = request.Void.VoidedAt;
            entity.VoidedByStaffId = request.Void.VoidedByStaffId;
            entity.VoidReason = request.Void.Reason;
        }

        return new TransactionDetail
        {
            Transaction = entity,
            Lines = request.Lines.Select(ToEntity).ToList(),
            Serials = request.Lines.SelectMany(static x => x.SerialNumbers.Select(serialNumber => new TransactionLineSerialEntity { TransactionLineId = x.Id, SerialNumber = serialNumber })).ToList(),
            Discounts = request.Discounts.Select(ToEntity).ToList(),
            TaxSummaries = request.TaxSummaries.Select(ToEntity).ToList(),
            Payments = request.Payments.Select(ToEntity).ToList(),
            Delivery = request.Delivery is null ? null : ToEntity(request.Delivery)
        };
    }

    //--------------------------------------------------------------------------------
    // Mapper: Entity -> Response
    //--------------------------------------------------------------------------------

    [Mapper]
    private static partial TransactionResponseItem ToResponseCore(TransactionEntity entity);

    [Mapper]
    private static partial TransactionResponseItemLine ToResponse(TransactionLineEntity entity);

    [Mapper]
    private static partial TransactionResponseItemDiscount ToResponse(TransactionDiscountEntity entity);

    [Mapper]
    private static partial TransactionResponseItemTaxSummary ToResponse(TransactionTaxSummaryEntity entity);

    [Mapper]
    private static partial TransactionResponseItemPayment ToResponse(TransactionPaymentEntity entity);

    [Mapper]
    private static partial TransactionResponseItemDelivery ToResponse(TransactionDeliveryEntity entity);

    // 明細・値引・税・支払・配送・シリアルを集めて応答にする
    internal static TransactionResponseItem ToResponse(TransactionDetail detail, IReadOnlyList<RuleWarning>? warnings = null)
    {
        var entity = detail.Transaction;
        var serialsByLine = detail.Serials.ToLookup(static x => x.TransactionLineId, static x => x.SerialNumber);
        var response = ToResponseCore(entity);
        response.Lines = detail.Lines.Select(x =>
        {
            var line = ToResponse(x);
            line.SerialNumbers = serialsByLine[x.Id].ToList();
            return line;
        }).ToList();
        response.Discounts = detail.Discounts.Select(ToResponse).ToList();
        response.TaxSummaries = detail.TaxSummaries.Select(ToResponse).ToList();
        response.Payments = detail.Payments.Select(ToResponse).ToList();
        response.Delivery = detail.Delivery is null ? null : ToResponse(detail.Delivery);
        response.Void = entity.VoidedAt is null
            ? null
            : new TransactionResponseItemVoid { VoidedAt = entity.VoidedAt.Value, VoidedByStaffId = entity.VoidedByStaffId ?? Guid.Empty, Reason = entity.VoidReason ?? String.Empty };
        if (warnings is not null)
        {
            response.Warnings = warnings.Select(static x => new TransactionResponseItemWarning { Code = x.Code.ToCode(), Message = RuleText.Of(x.Code), LineId = x.LineId }).ToList();
        }

        return response;
    }

    // 計算結果 (Pos.Domain) を応答にする
    private static TransactionCalculationResponse ToResponse(SalesResult result) => new()
    {
        Lines = result.Lines.Select(static x => new TransactionCalculationResponseLine
        {
            Id = x.Id,
            Amount = x.Amount,
            DiscountAmount = x.DiscountAmount,
            AllocatedDiscountAmount = x.AllocatedDiscountAmount,
            NetAmount = x.NetAmount,
            PointsRedeemed = x.PointsRedeemed,
            PointsEarned = x.PointsEarned
        }).ToList(),
        Discounts = result.Discounts.Select(static x => new TransactionCalculationResponseDiscount { Id = x.Id, Amount = x.Amount }).ToList(),
        TaxSummaries = result.TaxSummaries.Select(static x => new TransactionCalculationResponseTaxSummary
        {
            TaxRateId = x.TaxRateId,
            Rate = x.Rate,
            TaxIncluded = x.TaxIncluded,
            TaxableAmount = x.TaxableAmount,
            TaxAmount = x.TaxAmount
        }).ToList(),
        Subtotal = result.Subtotal,
        DiscountTotal = result.DiscountTotal,
        NetSubtotal = result.NetSubtotal,
        TaxTotal = result.TaxTotal,
        Total = result.Total,
        TenderedTotal = result.TenderedTotal,
        ChangeAmount = result.ChangeAmount,
        PointsEarned = result.PointsEarned,
        PointsRedeemed = result.PointsRedeemed
    };

    private static IResult ToProblem(TransactionValidation validation) =>
        ApiProblems.FromValidation(validation, validation.Expected is null ? null : ToResponse(validation.Expected));

    //--------------------------------------------------------------------------------
    // Create
    //--------------------------------------------------------------------------------

    // 冪等: 同じ id は 200 で既存を返す (内容が違えば 409)
    private static async ValueTask<IResult> HandleCreateAsync(
        TransactionService service,
        TransactionRequest request,
        CancellationToken cancellationToken)
    {
        var detail = ToDetail(request);
        var result = await service.RegisterAsync(detail, cancellationToken);
        return result.Status switch
        {
            TransactionResultStatus.Success => TypedResults.Created($"{ApiRoutes.Transactions}/{detail.Transaction.Id}", ToResponse(result.Detail!, result.Warnings)),
            TransactionResultStatus.Existing => TypedResults.Ok(ToResponse(result.Detail!)),
            TransactionResultStatus.DuplicateMismatch => ApiProblems.DuplicateIdMismatch(),
            TransactionResultStatus.Invalid => ToProblem(result.Validation!),
            _ => ApiProblems.FromViolation(result.Violation!)
        };
    }

    //--------------------------------------------------------------------------------
    // Calculate
    //--------------------------------------------------------------------------------

    // 入力項目だけを送り、計算項目を返す (登録しない)
    private static async ValueTask<IResult> HandleCalculateAsync(
        TransactionService service,
        TransactionCalculateRequest request,
        CancellationToken cancellationToken)
    {
        var calculation = await service.CalculateAsync(
            request.Type,
            request.OriginalTransactionId,
            request.Lines.Select(ToEntity).ToList(),
            request.Discounts.Select(ToEntity).ToList(),
            request.Payments.Select(ToEntity).ToList(),
            cancellationToken);
        return calculation.Error is not null ? ApiProblems.FromViolation(calculation.Error) : TypedResults.Ok(ToResponse(calculation.Result!));
    }

    //--------------------------------------------------------------------------------
    // Query
    //--------------------------------------------------------------------------------

    private static async ValueTask<IResult> HandleListAsync(
        TransactionService service,
        Guid? storeId,
        Guid? terminalId,
        Guid? staffId,
        Guid? shiftId,
        Guid? customerId,
        DateOnly? from,
        DateOnly? to,
        TransactionType? type,
        TransactionStatus? status,
        string? sort,
        CancellationToken cancellationToken,
        bool desc = true,
        [Range(0, Int32.MaxValue)] int page = 0,
        [Range(1, ApiDefaults.MaxPageSize)] int size = ApiDefaults.PageSize)
    {
        var parameter = new TransactionQueryParameter
        {
            StoreId = storeId,
            TerminalId = terminalId,
            StaffId = staffId,
            ShiftId = shiftId,
            CustomerId = customerId,
            From = from,
            To = to,
            Type = type,
            Status = status,
            Sort = sort,
            Desc = desc,
            Page = page,
            Size = size
        };
        var result = await service.QueryDetailPageAsync(parameter, cancellationToken);
        return TypedResults.Ok(new TransactionResponse { Total = result.Total, Page = result.Page, Size = result.Size, Items = result.Items.Select(static x => ToResponse(x)).ToList() });
    }

    // 返品時のレシート番号検索
    private static async ValueTask<IResult> HandleLookupAsync(
        TransactionService service,
        string? receiptNo,
        CancellationToken cancellationToken)
    {
        if (String.IsNullOrEmpty(receiptNo))
        {
            return ApiProblems.BadRequest("receiptNo を指定してください");
        }

        var detail = await service.QueryDetailByReceiptNoAsync(receiptNo, cancellationToken);
        return detail is null ? ApiProblems.NotFound("取引が見つかりません") : TypedResults.Ok(ToResponse(detail));
    }

    private static async ValueTask<IResult> HandleGetAsync(
        TransactionService service,
        Guid id,
        CancellationToken cancellationToken)
    {
        var detail = await service.QueryDetailAsync(id, cancellationToken);
        return detail is null ? ApiProblems.NotFound() : TypedResults.Ok(ToResponse(detail));
    }

    //--------------------------------------------------------------------------------
    // Void
    //--------------------------------------------------------------------------------

    // 取消: Status を Voided にし、在庫は逆方向の履歴、ポイントは Void 履歴を追加する
    private static async ValueTask<IResult> HandleVoidAsync(
        TransactionService service,
        Guid id,
        TransactionVoidRequest request,
        CancellationToken cancellationToken)
    {
        var result = await service.VoidAsync(id, request.VoidedAt, request.StaffId, request.Reason, cancellationToken);
        return result.Status switch
        {
            TransactionResultStatus.Success => TypedResults.Ok(ToResponse(result.Detail!)),
            TransactionResultStatus.NotFound => ApiProblems.NotFound("取引が見つかりません"),
            TransactionResultStatus.Invalid => ToProblem(result.Validation!),
            _ => ApiProblems.FromViolation(result.Violation!)
        };
    }
}
