namespace Pos.Server.Host.Mappers;

using Pos.Domain.Sales;
using Pos.Server.Accessors;
using Pos.Server.Models.Entity;
using Pos.Shared.Transactions;

using Smart.Mapper;

// 取引の Entity ↔ Request / Response と、Domain の計算入力への変換
public static partial class TransactionMapper
{
    //--------------------------------------------------------------------------------
    // Entity -> Response
    //--------------------------------------------------------------------------------

    [Mapper]
    private static partial TransactionResponse ToResponseCore(TransactionEntity entity);

    [Mapper]
    private static partial TransactionResponseLine ToResponseLine(TransactionLineEntity entity);

    [Mapper]
    public static partial TransactionResponseDiscount ToResponseDiscount(TransactionDiscountEntity entity);

    [Mapper]
    public static partial TransactionResponseTaxSummary ToResponseTaxSummary(TransactionTaxSummaryEntity entity);

    [Mapper]
    public static partial TransactionResponsePayment ToResponsePayment(TransactionPaymentEntity entity);

    [Mapper]
    public static partial TransactionResponseDelivery ToResponseDelivery(TransactionDeliveryEntity entity);

    // 明細・値引・税・支払・配送・シリアルを集めて応答にする
    public static async ValueTask<TransactionResponse> ToResponseAsync(TransactionAccessor accessor, TransactionEntity entity, CancellationToken cancellationToken)
    {
        var lines = await accessor.QueryLinesAsync(entity.Id, cancellationToken);
        var serials = await accessor.QueryLineSerialsAsync(entity.Id, cancellationToken);
        var discounts = await accessor.QueryDiscountsAsync(entity.Id, cancellationToken);
        var taxSummaries = await accessor.QueryTaxSummariesAsync(entity.Id, cancellationToken);
        var payments = await accessor.QueryPaymentsAsync(entity.Id, cancellationToken);
        var delivery = await accessor.QueryDeliveryAsync(entity.Id, cancellationToken);

        var serialsByLine = serials.ToLookup(static x => x.TransactionLineId, static x => x.SerialNumber);
        var response = ToResponseCore(entity);
        response.Lines = lines.Select(x =>
        {
            var line = ToResponseLine(x);
            line.SerialNumbers = serialsByLine[x.Id].ToList();
            return line;
        }).ToList();
        response.Discounts = discounts.Select(ToResponseDiscount).ToList();
        response.TaxSummaries = taxSummaries.Select(ToResponseTaxSummary).ToList();
        response.Payments = payments.Select(ToResponsePayment).ToList();
        response.Delivery = delivery is null ? null : ToResponseDelivery(delivery);
        response.Void = entity.VoidedAt is null
            ? null
            : new TransactionResponseVoid { VoidedAt = entity.VoidedAt.Value, VoidedByStaffId = entity.VoidedByStaffId ?? Guid.Empty, Reason = entity.VoidReason ?? String.Empty };
        return response;
    }

    //--------------------------------------------------------------------------------
    // Request -> Entity
    //--------------------------------------------------------------------------------

    [Mapper]
    public static partial TransactionEntity ToEntity(TransactionRequest request);

    [Mapper]
    public static partial TransactionLineEntity ToLineEntity(TransactionRequestLine request);

    [Mapper]
    public static partial TransactionDiscountEntity ToDiscountEntity(TransactionRequestDiscount request);

    [Mapper]
    public static partial TransactionTaxSummaryEntity ToTaxSummaryEntity(TransactionRequestTaxSummary request);

    [Mapper]
    public static partial TransactionPaymentEntity ToPaymentEntity(TransactionRequestPayment request);

    [Mapper]
    public static partial TransactionDeliveryEntity ToDeliveryEntity(TransactionRequestDelivery request);

    //--------------------------------------------------------------------------------
    // Request -> Domain (計算入力と端末の計算結果)
    //--------------------------------------------------------------------------------

    public static SalesInput ToSalesInput(TransactionCalculateRequest request, TaxRounding taxRounding, PointBasis pointBasis, IReadOnlyDictionary<Guid, PaymentMethodEntity> paymentMethods) => new()
    {
        TaxRounding = taxRounding,
        PointBasis = pointBasis,
        Lines = request.Lines.Select(static x => new SalesInputLine
        {
            Id = x.Id,
            LineNo = x.LineNo,
            ProductId = x.ProductId,
            ListPrice = x.ListPrice,
            UnitPrice = x.UnitPrice,
            Quantity = x.Quantity,
            TaxRateId = x.TaxRateId,
            TaxRate = x.TaxRate,
            TaxIncluded = x.TaxIncluded,
            PointRate = x.PointRate
        }).ToList(),
        Discounts = request.Discounts.Select(static x => new SalesInputDiscount { Id = x.Id, LineId = x.LineId, Type = x.Type, Value = x.Value }).ToList(),
        Payments = ToPayments(request.Payments, paymentMethods)
    };

    public static ReturnInput ToReturnInput(TransactionCalculateRequest request, TaxRounding taxRounding, IEnumerable<TransactionLineEntity> originalLines, IReadOnlyDictionary<Guid, PaymentMethodEntity> paymentMethods) => new()
    {
        TaxRounding = taxRounding,
        OriginalLines = originalLines.Select(static x => new ReturnOriginalLine
        {
            Id = x.Id,
            UnitPrice = x.UnitPrice,
            Quantity = x.Quantity,
            ReturnedQuantity = x.ReturnedQuantity,
            DiscountAmount = x.DiscountAmount,
            AllocatedDiscountAmount = x.AllocatedDiscountAmount,
            PointsEarned = x.PointsEarned,
            PointsRedeemed = x.PointsRedeemed,
            TaxRateId = x.TaxRateId,
            TaxRate = x.TaxRate,
            TaxIncluded = x.TaxIncluded
        }).ToList(),
        Lines = request.Lines.Select(static x => new ReturnInputLine { Id = x.Id, LineNo = x.LineNo, OriginalLineId = x.OriginalLineId ?? Guid.Empty, Quantity = x.Quantity }).ToList(),
        Payments = ToPayments(request.Payments, paymentMethods)
    };

    private static List<SalesInputPayment> ToPayments(IEnumerable<TransactionRequestPayment> payments, IReadOnlyDictionary<Guid, PaymentMethodEntity> paymentMethods) =>
        payments.Select(x => new SalesInputPayment
        {
            Id = x.Id,
            Kind = x.Kind,
            Amount = x.Amount,
            TenderedAmount = x.TenderedAmount,
            AllowsChange = paymentMethods.TryGetValue(x.PaymentMethodId, out var method) && method.AllowsChange
        }).ToList();

    // 端末が送った計算項目
    public static SalesResult ToClaimedResult(TransactionRequest request) => new()
    {
        Lines = request.Lines.Select(static x => new SalesResultLine
        {
            Id = x.Id,
            LineNo = x.LineNo,
            Amount = x.Amount,
            DiscountAmount = x.DiscountAmount,
            AllocatedDiscountAmount = x.AllocatedDiscountAmount,
            NetAmount = x.NetAmount,
            PointsRedeemed = x.PointsRedeemed,
            PointsEarned = x.PointsEarned
        }).ToList(),
        Discounts = request.Discounts.Select(static x => new SalesResultDiscount { Id = x.Id, Amount = x.Amount }).ToList(),
        TaxSummaries = request.TaxSummaries.Select(static x => new SalesResultTaxSummary
        {
            TaxRateId = x.TaxRateId,
            Rate = x.Rate,
            TaxIncluded = x.TaxIncluded,
            TaxableAmount = x.TaxableAmount,
            TaxAmount = x.TaxAmount
        }).ToList(),
        Subtotal = request.Subtotal,
        DiscountTotal = request.DiscountTotal,
        NetSubtotal = request.NetSubtotal,
        TaxTotal = request.TaxTotal,
        Total = request.Total,
        TenderedTotal = request.TenderedTotal,
        ChangeAmount = request.ChangeAmount,
        PointsEarned = request.PointsEarned,
        PointsRedeemed = request.PointsRedeemed
    };

    //--------------------------------------------------------------------------------
    // Domain -> Response
    //--------------------------------------------------------------------------------

    public static TransactionCalculationResponse ToCalculationResponse(SalesResult result) => new()
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

    // TransactionRequest から計算入力用の形へ (計算項目は無視される)
    public static TransactionCalculateRequest ToCalculateRequest(TransactionRequest request) => new()
    {
        Type = request.Type,
        OriginalTransactionId = request.OriginalTransactionId,
        Lines = request.Lines,
        Discounts = request.Discounts,
        Payments = request.Payments
    };
}
