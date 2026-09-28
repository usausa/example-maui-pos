namespace Pos.Terminal.Usecases;

using Pos.Contract.Transactions;
using Pos.Domain.Logic;
using Pos.Terminal.Models.Cart;

using Smart.Mapper;

// 取引の文脈 (店舗・端末・担当・シフト・レシート番号)
public sealed record TransactionContext(
    Guid StoreId,
    Guid TerminalId,
    Guid StaffId,
    Guid ShiftId,
    string ReceiptNo,
    DateOnly BusinessDate,
    DateTime TransactedAt);

// Cart → Pos.Domain の計算入力 → TransactionCreateRequest (端末が計算した項目を含む)。サーバは同じ計算で検証する
public static partial class TransactionMapper
{
    // 店舗・端末・担当・シフトが揃っているとき (Session.CanTransact) だけ呼ぶ
    public static TransactionContext CreateContext(Session session, string receiptNo, DateTime transactedAt)
    {
        return new TransactionContext(session.Store!.Id, session.Terminal!.Id, session.Staff!.Id, session.CurrentShift!.Id, receiptNo, session.BusinessDate, transactedAt);
    }

    //--------------------------------------------------------------------------------
    // Sale
    //--------------------------------------------------------------------------------

    // 会員がいないときはポイントを付けない (サーバの CUSTOMER_REQUIRED を避ける)
    public static SalesInput ToSalesInput(SalesCart cart, IEnumerable<CartPayment> payments, TaxRounding taxRounding, PointBasis pointBasis)
    {
        var hasCustomer = cart.Customer is not null;
        var lines = new List<SalesInputLine>(cart.Lines.Count);
        var discounts = new List<SalesInputDiscount>();
        for (var i = 0; i < cart.Lines.Count; i++)
        {
            var line = cart.Lines[i];
            lines.Add(new SalesInputLine
            {
                Id = line.Id,
                LineNo = i + 1,
                ProductId = line.Product.Id,
                ListPrice = line.Product.Price,
                UnitPrice = line.UnitPrice,
                Quantity = line.Quantity,
                TaxRateId = line.TaxRate.Id,
                TaxRate = line.TaxRate.Rate,
                TaxIncluded = line.Product.TaxIncluded,
                PointRate = hasCustomer ? line.Product.PointRate : 0m
            });
            discounts.AddRange(line.Discounts.Select(x => new SalesInputDiscount { Id = x.Id, LineId = line.Id, Type = x.Type, Value = x.Value }));
        }

        discounts.AddRange(cart.Discounts.Select(static x => new SalesInputDiscount { Id = x.Id, Type = x.Type, Value = x.Value }));

        return new SalesInput
        {
            TaxRounding = taxRounding,
            PointBasis = pointBasis,
            Lines = lines,
            Discounts = discounts,
            Payments = payments.Select(static x => new SalesInputPayment { Id = x.Id, Kind = x.Method.Kind, Amount = x.Amount, TenderedAmount = x.TenderedAmount, AllowsChange = x.Method.AllowsChange }).ToList()
        };
    }

    public static TransactionCreateRequest ToRequest(SalesCart cart, IEnumerable<CartPayment> payments, SalesResult result, TransactionContext context)
    {
        var hasCustomer = cart.Customer is not null;
        var lines = new List<TransactionCreateRequestLine>(cart.Lines.Count);
        var discounts = new List<TransactionCreateRequestDiscount>();
        for (var i = 0; i < cart.Lines.Count; i++)
        {
            var line = cart.Lines[i];
            var calculated = result.Lines[i];
            lines.Add(new TransactionCreateRequestLine
            {
                Id = line.Id,
                LineNo = i + 1,
                ProductId = line.Product.Id,
                ProductCode = line.Product.Code,
                ProductName = line.Product.Name,
                CategoryId = line.Product.CategoryId,
                Kind = line.Product.Kind,
                ListPrice = line.Product.Price,
                UnitPrice = line.UnitPrice,
                Quantity = line.Quantity,
                TaxRateId = line.TaxRate.Id,
                TaxRate = line.TaxRate.Rate,
                TaxIncluded = line.Product.TaxIncluded,
                PointRate = hasCustomer ? line.Product.PointRate : 0m,
                Amount = calculated.Amount,
                DiscountAmount = calculated.DiscountAmount,
                AllocatedDiscountAmount = calculated.AllocatedDiscountAmount,
                NetAmount = calculated.NetAmount,
                PointsRedeemed = calculated.PointsRedeemed,
                PointsEarned = calculated.PointsEarned,
                SerialNumbers = line.SerialNumbers.ToList(),
                Note = line.Note
            });
            discounts.AddRange(line.Discounts.Select(x => ToRequestDiscount(x, line.Id, result)));
        }

        discounts.AddRange(cart.Discounts.Select(x => ToRequestDiscount(x, null, result)));

        return new TransactionCreateRequest
        {
            Id = Guid.CreateVersion7(),
            Type = TransactionType.Sale,
            Status = TransactionStatus.Completed,
            StoreId = context.StoreId,
            TerminalId = context.TerminalId,
            StaffId = context.StaffId,
            ShiftId = context.ShiftId,
            CustomerId = cart.Customer?.Id,
            ReceiptNo = context.ReceiptNo,
            BusinessDate = context.BusinessDate,
            TransactedAt = context.TransactedAt,
            Lines = lines,
            Discounts = discounts,
            TaxSummaries = ToTaxSummaries(result),
            Subtotal = result.Subtotal,
            DiscountTotal = result.DiscountTotal,
            NetSubtotal = result.NetSubtotal,
            TaxTotal = result.TaxTotal,
            Total = result.Total,
            Payments = ToRequestPayments(payments),
            TenderedTotal = result.TenderedTotal,
            ChangeAmount = result.ChangeAmount,
            PointsEarned = result.PointsEarned,
            PointsRedeemed = result.PointsRedeemed,
            OrderId = cart.OrderId,
            Delivery = cart.Delivery is null ? null : ToRequestDelivery(cart.Delivery),
            Note = cart.Note
        };
    }

    [Mapper]
    private static partial TransactionCreateRequestDelivery ToRequestDelivery(CartDelivery delivery);

    private static TransactionCreateRequestDiscount ToRequestDiscount(CartDiscount discount, Guid? lineId, SalesResult result) => new()
    {
        Id = discount.Id,
        LineId = lineId,
        DiscountId = discount.DiscountId,
        Name = discount.Name,
        Type = discount.Type,
        Value = discount.Value,
        Amount = result.Discounts.First(x => x.Id == discount.Id).Amount,
        Reason = discount.Reason,
        ApprovedByStaffId = discount.ApprovedByStaffId
    };

    //--------------------------------------------------------------------------------
    // Return
    //--------------------------------------------------------------------------------

    public static ReturnInput ToReturnInput(TransactionResponseItem original, IEnumerable<(TransactionResponseLine Line, decimal Quantity)> returns, IEnumerable<CartPayment> payments, TaxRounding taxRounding)
    {
        return new ReturnInput
        {
            TaxRounding = taxRounding,
            OriginalLines = original.Lines.Select(ToOriginalLine).ToList(),
            Lines = returns.Select((x, i) => new ReturnInputLine { Id = Guid.CreateVersion7(), LineNo = i + 1, OriginalLineId = x.Line.Id, Quantity = x.Quantity }).ToList(),
            Payments = payments.Select(static x => new SalesInputPayment { Id = x.Id, Kind = x.Method.Kind, Amount = x.Amount, TenderedAmount = x.TenderedAmount, AllowsChange = x.Method.AllowsChange }).ToList()
        };
    }

    [Mapper]
    private static partial ReturnOriginalLine ToOriginalLine(TransactionResponseLine line);

    public static TransactionCreateRequest ToReturnRequest(TransactionResponseItem original, IReadOnlyList<(TransactionResponseLine Line, decimal Quantity)> returns, IEnumerable<CartPayment> payments, SalesResult result, TransactionContext context, string? note)
    {
        var lines = new List<TransactionCreateRequestLine>(returns.Count);
        for (var i = 0; i < returns.Count; i++)
        {
            var (line, quantity) = returns[i];
            var calculated = result.Lines[i];
            lines.Add(new TransactionCreateRequestLine
            {
                Id = Guid.CreateVersion7(),
                LineNo = i + 1,
                ProductId = line.ProductId,
                ProductCode = line.ProductCode,
                ProductName = line.ProductName,
                CategoryId = line.CategoryId,
                Kind = line.Kind,
                ListPrice = line.ListPrice,
                UnitPrice = line.UnitPrice,
                Quantity = quantity,
                TaxRateId = line.TaxRateId,
                TaxRate = line.TaxRate,
                TaxIncluded = line.TaxIncluded,
                PointRate = line.PointRate,
                Amount = calculated.Amount,
                DiscountAmount = calculated.DiscountAmount,
                AllocatedDiscountAmount = calculated.AllocatedDiscountAmount,
                NetAmount = calculated.NetAmount,
                PointsRedeemed = calculated.PointsRedeemed,
                PointsEarned = calculated.PointsEarned,
                OriginalLineId = line.Id
            });
        }

        return new TransactionCreateRequest
        {
            Id = Guid.CreateVersion7(),
            Type = TransactionType.Return,
            Status = TransactionStatus.Completed,
            StoreId = context.StoreId,
            TerminalId = context.TerminalId,
            StaffId = context.StaffId,
            ShiftId = context.ShiftId,
            CustomerId = original.CustomerId,
            ReceiptNo = context.ReceiptNo,
            BusinessDate = context.BusinessDate,
            TransactedAt = context.TransactedAt,
            OriginalTransactionId = original.Id,
            Lines = lines,
            Discounts = [],
            TaxSummaries = ToTaxSummaries(result),
            Subtotal = result.Subtotal,
            DiscountTotal = result.DiscountTotal,
            NetSubtotal = result.NetSubtotal,
            TaxTotal = result.TaxTotal,
            Total = result.Total,
            Payments = ToRequestPayments(payments),
            TenderedTotal = result.TenderedTotal,
            ChangeAmount = result.ChangeAmount,
            PointsEarned = result.PointsEarned,
            PointsRedeemed = result.PointsRedeemed,
            Note = note
        };
    }

    //--------------------------------------------------------------------------------
    // Common
    //--------------------------------------------------------------------------------

    private static List<TransactionCreateRequestTaxSummary> ToTaxSummaries(SalesResult result) =>
        result.TaxSummaries.Select(ToRequestTaxSummary).ToList();

    [Mapper]
    private static partial TransactionCreateRequestTaxSummary ToRequestTaxSummary(SalesResultTaxSummary summary);

    private static List<TransactionCreateRequestPayment> ToRequestPayments(IEnumerable<CartPayment> payments) =>
        payments.Select(static (x, i) => new TransactionCreateRequestPayment { Id = x.Id, SeqNo = i + 1, PaymentMethodId = x.Method.Id, Kind = x.Method.Kind, Amount = x.Amount, TenderedAmount = x.TenderedAmount, Reference = x.Reference }).ToList();

    // 端末側の履歴用にサーバ応答と同じ形へ (送信後はサーバの応答で置き換える)。端末が作る要求は取消を持たない
    [Mapper]
    [MapCollection(nameof(TransactionResponseItem.Lines), Mapper = nameof(ToResponse))]
    [MapCollection(nameof(TransactionResponseItem.Discounts), Mapper = nameof(ToResponse))]
    [MapCollection(nameof(TransactionResponseItem.TaxSummaries), Mapper = nameof(ToResponse))]
    [MapCollection(nameof(TransactionResponseItem.Payments), Mapper = nameof(ToResponse))]
    [MapNested(nameof(TransactionResponseItem.Delivery), Mapper = nameof(ToResponse))]
    [MapIgnore(nameof(TransactionResponseItem.Void))]
    [MapProperty(nameof(TransactionResponseItem.CreatedAt), nameof(TransactionCreateRequest.TransactedAt))]
    [MapProperty(nameof(TransactionResponseItem.UpdatedAt), nameof(TransactionCreateRequest.TransactedAt))]
    public static partial TransactionResponseItem ToResponse(TransactionCreateRequest request);

    [Mapper]
    private static partial TransactionResponseLine ToResponse(TransactionCreateRequestLine line);

    [Mapper]
    private static partial TransactionResponseDiscount ToResponse(TransactionCreateRequestDiscount discount);

    [Mapper]
    private static partial TransactionResponseTaxSummary ToResponse(TransactionCreateRequestTaxSummary summary);

    [Mapper]
    private static partial TransactionResponsePayment ToResponse(TransactionCreateRequestPayment payment);

    [Mapper]
    private static partial TransactionResponseDelivery ToResponse(TransactionCreateRequestDelivery delivery);
}
