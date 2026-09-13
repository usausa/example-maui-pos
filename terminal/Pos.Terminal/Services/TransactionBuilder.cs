namespace Pos.Terminal.Services;

using Pos.Domain.Sales;
using Pos.Shared.Transactions;
using Pos.Terminal.Models.Sales;

// 取引の文脈 (店舗・端末・担当・シフト・レシート番号)
public sealed record TransactionContext(
    Guid StoreId,
    Guid TerminalId,
    Guid StaffId,
    Guid ShiftId,
    string ReceiptNo,
    DateOnly BusinessDate,
    DateTime TransactedAt);

// Cart → Pos.Domain の計算入力 → TransactionRequest (端末が計算した項目を含む)。サーバは同じ計算で検証する
public static class TransactionBuilder
{
    //--------------------------------------------------------------------------------
    // Sale
    //--------------------------------------------------------------------------------

    // 会員がいないときはポイントを付けない (サーバの CUSTOMER_REQUIRED を避ける)
    public static SalesInput ToSalesInput(Cart cart, IReadOnlyList<CartPayment> payments, TaxRounding taxRounding, PointBasis pointBasis)
    {
        ArgumentNullException.ThrowIfNull(cart);
        ArgumentNullException.ThrowIfNull(payments);

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

    public static TransactionRequest ToRequest(Cart cart, IReadOnlyList<CartPayment> payments, SalesResult result, TransactionContext context)
    {
        ArgumentNullException.ThrowIfNull(cart);
        ArgumentNullException.ThrowIfNull(payments);
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(context);

        var hasCustomer = cart.Customer is not null;
        var lines = new List<TransactionRequestLine>(cart.Lines.Count);
        var discounts = new List<TransactionRequestDiscount>();
        for (var i = 0; i < cart.Lines.Count; i++)
        {
            var line = cart.Lines[i];
            var calculated = result.Lines[i];
            lines.Add(new TransactionRequestLine
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

        return new TransactionRequest
        {
            Id = Guid.NewGuid(),
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
            Delivery = cart.Delivery is null ? null : new TransactionRequestDelivery
            {
                RecipientName = cart.Delivery.RecipientName,
                Phone = cart.Delivery.Phone,
                PostalCode = cart.Delivery.PostalCode,
                Address = cart.Delivery.Address,
                RequestedDate = cart.Delivery.RequestedDate,
                TimeSlot = cart.Delivery.TimeSlot,
                Note = cart.Delivery.Note
            },
            Note = cart.Note
        };
    }

    private static TransactionRequestDiscount ToRequestDiscount(CartDiscount discount, Guid? lineId, SalesResult result) => new()
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

    public static ReturnInput ToReturnInput(TransactionResponse original, IReadOnlyList<(TransactionResponseLine Line, decimal Quantity)> returns, IReadOnlyList<CartPayment> payments, TaxRounding taxRounding)
    {
        ArgumentNullException.ThrowIfNull(original);
        ArgumentNullException.ThrowIfNull(returns);
        ArgumentNullException.ThrowIfNull(payments);

        return new ReturnInput
        {
            TaxRounding = taxRounding,
            OriginalLines = original.Lines.Select(static x => new ReturnOriginalLine
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
            Lines = returns.Select((x, i) => new ReturnInputLine { Id = Guid.NewGuid(), LineNo = i + 1, OriginalLineId = x.Line.Id, Quantity = x.Quantity }).ToList(),
            Payments = payments.Select(static x => new SalesInputPayment { Id = x.Id, Kind = x.Method.Kind, Amount = x.Amount, TenderedAmount = x.TenderedAmount, AllowsChange = x.Method.AllowsChange }).ToList()
        };
    }

    public static TransactionRequest ToReturnRequest(TransactionResponse original, IReadOnlyList<(TransactionResponseLine Line, decimal Quantity)> returns, IReadOnlyList<CartPayment> payments, SalesResult result, TransactionContext context, string? note)
    {
        ArgumentNullException.ThrowIfNull(original);
        ArgumentNullException.ThrowIfNull(returns);
        ArgumentNullException.ThrowIfNull(payments);
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(context);

        var lines = new List<TransactionRequestLine>(returns.Count);
        for (var i = 0; i < returns.Count; i++)
        {
            var (line, quantity) = returns[i];
            var calculated = result.Lines[i];
            lines.Add(new TransactionRequestLine
            {
                Id = Guid.NewGuid(),
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

        return new TransactionRequest
        {
            Id = Guid.NewGuid(),
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

    private static List<TransactionRequestTaxSummary> ToTaxSummaries(SalesResult result) =>
        result.TaxSummaries.Select(static x => new TransactionRequestTaxSummary { TaxRateId = x.TaxRateId, Rate = x.Rate, TaxIncluded = x.TaxIncluded, TaxableAmount = x.TaxableAmount, TaxAmount = x.TaxAmount }).ToList();

    private static List<TransactionRequestPayment> ToRequestPayments(IEnumerable<CartPayment> payments) =>
        payments.Select(static (x, i) => new TransactionRequestPayment { Id = x.Id, SeqNo = i + 1, PaymentMethodId = x.Method.Id, Kind = x.Method.Kind, Amount = x.Amount, TenderedAmount = x.TenderedAmount, Reference = x.Reference }).ToList();

    // 端末側の履歴用にサーバ応答と同じ形へ (送信後はサーバの応答で置き換える)
    public static TransactionResponse ToResponse(TransactionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        return new TransactionResponse
        {
            Id = request.Id,
            Type = request.Type,
            Status = request.Status,
            StoreId = request.StoreId,
            TerminalId = request.TerminalId,
            StaffId = request.StaffId,
            ShiftId = request.ShiftId,
            CustomerId = request.CustomerId,
            ReceiptNo = request.ReceiptNo,
            BusinessDate = request.BusinessDate,
            TransactedAt = request.TransactedAt,
            OriginalTransactionId = request.OriginalTransactionId,
            Lines = request.Lines.Select(static x => new TransactionResponseLine
            {
                Id = x.Id,
                LineNo = x.LineNo,
                ProductId = x.ProductId,
                ProductCode = x.ProductCode,
                ProductName = x.ProductName,
                CategoryId = x.CategoryId,
                Kind = x.Kind,
                ListPrice = x.ListPrice,
                UnitPrice = x.UnitPrice,
                Quantity = x.Quantity,
                TaxRateId = x.TaxRateId,
                TaxRate = x.TaxRate,
                TaxIncluded = x.TaxIncluded,
                PointRate = x.PointRate,
                Amount = x.Amount,
                DiscountAmount = x.DiscountAmount,
                AllocatedDiscountAmount = x.AllocatedDiscountAmount,
                NetAmount = x.NetAmount,
                PointsRedeemed = x.PointsRedeemed,
                PointsEarned = x.PointsEarned,
                SerialNumbers = x.SerialNumbers,
                OriginalLineId = x.OriginalLineId,
                Note = x.Note
            }).ToList(),
            Discounts = request.Discounts.Select(static x => new TransactionResponseDiscount { Id = x.Id, LineId = x.LineId, DiscountId = x.DiscountId, Name = x.Name, Type = x.Type, Value = x.Value, Amount = x.Amount, Reason = x.Reason, ApprovedByStaffId = x.ApprovedByStaffId }).ToList(),
            TaxSummaries = request.TaxSummaries.Select(static x => new TransactionResponseTaxSummary { TaxRateId = x.TaxRateId, Rate = x.Rate, TaxIncluded = x.TaxIncluded, TaxableAmount = x.TaxableAmount, TaxAmount = x.TaxAmount }).ToList(),
            Subtotal = request.Subtotal,
            DiscountTotal = request.DiscountTotal,
            NetSubtotal = request.NetSubtotal,
            TaxTotal = request.TaxTotal,
            Total = request.Total,
            Payments = request.Payments.Select(static x => new TransactionResponsePayment { Id = x.Id, SeqNo = x.SeqNo, PaymentMethodId = x.PaymentMethodId, Kind = x.Kind, Amount = x.Amount, TenderedAmount = x.TenderedAmount, Reference = x.Reference, Note = x.Note }).ToList(),
            TenderedTotal = request.TenderedTotal,
            ChangeAmount = request.ChangeAmount,
            PointsEarned = request.PointsEarned,
            PointsRedeemed = request.PointsRedeemed,
            Delivery = request.Delivery is null ? null : new TransactionResponseDelivery
            {
                RecipientName = request.Delivery.RecipientName,
                Phone = request.Delivery.Phone,
                PostalCode = request.Delivery.PostalCode,
                Address = request.Delivery.Address,
                RequestedDate = request.Delivery.RequestedDate,
                TimeSlot = request.Delivery.TimeSlot,
                Note = request.Delivery.Note
            },
            Note = request.Note,
            CreatedAt = request.TransactedAt,
            UpdatedAt = request.TransactedAt
        };
    }
}
