namespace Pos.Domain.Logic;

public sealed class TransactionLogicTests
{
    private static readonly Guid StoreId = new("00000000-0000-0000-0001-000000000001");

    private static readonly Guid TerminalId = new("00000000-0000-0000-0006-000000000001");

    private static readonly Guid OtherTerminalId = new("00000000-0000-0000-0006-000000000002");

    private static readonly Guid ShiftId = new("00000000-0000-0000-0007-000000000001");

    private static readonly Guid OriginalTransactionId = new("00000000-0000-0000-0008-000000000001");

    private static readonly Guid ReturnLineId = new("00000000-0000-0000-0005-000000000001");

    // ------------------------------------------------------------
    // Sale
    // ------------------------------------------------------------

    [Fact]
    public void ValidateSaleAccepts()
    {
        var input = SalesExample.Input();
        var claimed = SalesLogic.Calculate(input);

        var validation = TransactionLogic.ValidateSale(SaleContext(), input, claimed);

        Assert.True(validation.IsValid);
        Assert.Empty(validation.Warnings);
        Assert.NotNull(validation.Expected);
        Assert.True(SalesLogic.Matches(claimed, validation.Expected));
    }

    [Fact]
    public void ValidateSaleShift()
    {
        var input = SalesExample.Input();
        var claimed = SalesLogic.Calculate(input);

        var notFound = TransactionLogic.ValidateSale(SaleContext(shiftMissing: true), input, claimed);
        Assert.Contains(notFound.Errors, static x => x.Code == ErrorCode.ShiftNotFound);

        var closed = TransactionLogic.ValidateSale(SaleContext(shift: Shift(ShiftStatus.Closed)), input, claimed);
        Assert.Contains(closed.Errors, static x => x.Code == ErrorCode.ShiftClosed);

        var mismatch = TransactionLogic.ValidateSale(SaleContext(shift: Shift(ShiftStatus.Open, OtherTerminalId)), input, claimed);
        Assert.Contains(mismatch.Errors, static x => x.Code == ErrorCode.ShiftTerminalMismatch);
    }

    [Fact]
    public void ValidateSaleDuplicateReceiptNo()
    {
        var input = SalesExample.Input();
        var claimed = SalesLogic.Calculate(input);

        var validation = TransactionLogic.ValidateSale(SaleContext(receiptNoInUse: true), input, claimed);

        Assert.Contains(validation.Errors, static x => x.Code == ErrorCode.DuplicateReceiptNo);
    }

    [Fact]
    public void ValidateSaleProductNotFound()
    {
        var input = SalesExample.Input();
        var claimed = SalesLogic.Calculate(input);
        var context = SaleContext(products: [Product(SalesExample.CameraProduct), Product(SalesExample.SdCardProduct)]);

        var validation = TransactionLogic.ValidateSale(context, input, claimed);

        var error = Assert.Single(validation.Errors);
        Assert.Equal(ErrorCode.ProductNotFound, error.Code);
        Assert.Equal(SalesExample.DeliveryLine, error.LineId);
    }

    // 売価変更不可の商品は unitPrice = listPrice
    [Fact]
    public void ValidateSalePriceOverrideNotAllowed()
    {
        var lines = SalesExample.Lines();
        lines[0] = lines[0] with { UnitPrice = 79000m };
        var payments = SalesExample.Payments();
        payments[2] = SalesExample.Cash(24150m, 30000m);
        var input = SalesExample.Input() with { Lines = lines, Payments = payments };
        var claimed = SalesLogic.Calculate(input);
        Assert.Equal(79150m, claimed.Total);

        var denied = TransactionLogic.ValidateSale(SaleContext(), input, claimed);
        var error = Assert.Single(denied.Errors);
        Assert.Equal(ErrorCode.PriceOverrideNotAllowed, error.Code);
        Assert.Equal(SalesExample.CameraLine, error.LineId);

        var allowed = TransactionLogic.ValidateSale(SaleContext(products: [Product(SalesExample.CameraProduct, allowsPriceOverride: true), Product(SalesExample.SdCardProduct), Product(SalesExample.DeliveryProduct)]), input, claimed);
        Assert.True(allowed.IsValid);
    }

    // 明細値引超過は検証エラーで、計算はしない
    [Fact]
    public void ValidateSaleLineDiscountExceeded()
    {
        var discounts = SalesExample.Discounts();
        discounts[0] = new SalesInputDiscount { Id = SalesExample.CameraDiscount, LineId = SalesExample.CameraLine, Type = DiscountType.Amount, Value = 90000m };
        var input = SalesExample.Input() with { Discounts = discounts };
        var claimed = SalesLogic.Calculate(SalesExample.Input());

        var validation = TransactionLogic.ValidateSale(SaleContext(), input, claimed);

        var error = Assert.Single(validation.Errors);
        Assert.Equal(ErrorCode.ValidationError, error.Code);
        Assert.Equal(SalesExample.CameraLine, error.LineId);
        Assert.Null(validation.Expected);
    }

    [Fact]
    public void ValidateSaleTransactionDiscountExceeded()
    {
        var discounts = SalesExample.Discounts();
        discounts[1] = new SalesInputDiscount { Id = SalesExample.TransactionDiscount, LineId = null, Type = DiscountType.Amount, Value = 90000m };
        var input = SalesExample.Input() with { Discounts = discounts };
        var claimed = SalesLogic.Calculate(SalesExample.Input());

        var validation = TransactionLogic.ValidateSale(SaleContext(), input, claimed);

        var error = Assert.Single(validation.Errors);
        Assert.Equal(ErrorCode.ValidationError, error.Code);
        Assert.Null(validation.Expected);
    }

    // 送信された計算項目が再計算と違えば CALCULATION_MISMATCH。expected に再計算結果
    [Fact]
    public void ValidateSaleCalculationMismatch()
    {
        var input = SalesExample.Input();
        var claimed = SalesLogic.Calculate(input) with { Total = 80000m };

        var validation = TransactionLogic.ValidateSale(SaleContext(), input, claimed);

        var error = Assert.Single(validation.Errors);
        Assert.Equal(ErrorCode.CalculationMismatch, error.Code);
        Assert.NotNull(validation.Expected);
        Assert.Equal(80100m, validation.Expected.Total);
    }

    // 支払合計 ≠ total
    [Fact]
    public void ValidateSalePaymentTotalMismatch()
    {
        var payments = SalesExample.Payments();
        payments[2] = SalesExample.Cash(25000m, 30000m);
        var input = SalesExample.Input() with { Payments = payments };
        var claimed = SalesLogic.Calculate(input);

        var validation = TransactionLogic.ValidateSale(SaleContext(), input, claimed);

        var error = Assert.Single(validation.Errors);
        Assert.Equal(ErrorCode.PaymentMismatch, error.Code);
    }

    // 釣銭が出せない支払方法で預り額 ≠ 充当額、または預り額 < 充当額
    [Fact]
    public void ValidateSaleChangeMismatch()
    {
        var payments = SalesExample.Payments();
        payments[1] = new SalesInputPayment { Id = SalesExample.CardPayment, Kind = PaymentKind.Card, Amount = 50000m, TenderedAmount = 60000m };
        var input = SalesExample.Input() with { Payments = payments };
        var validation = TransactionLogic.ValidateSale(SaleContext(), input, SalesLogic.Calculate(input));
        var error = Assert.Single(validation.Errors);
        Assert.Equal(ErrorCode.PaymentMismatch, error.Code);

        payments[1] = new SalesInputPayment { Id = SalesExample.CardPayment, Kind = PaymentKind.Card, Amount = 50000m, TenderedAmount = 50000m };
        payments[2] = SalesExample.Cash(25100m, 20000m);
        input = SalesExample.Input() with { Payments = payments };
        validation = TransactionLogic.ValidateSale(SaleContext(), input, SalesLogic.Calculate(input));
        Assert.Contains(validation.Errors, static x => x.Code == ErrorCode.PaymentMismatch);
    }

    // ポイントの付与・利用があるのに会員なし
    [Fact]
    public void ValidateSaleCustomerRequired()
    {
        var input = SalesExample.Input();
        var claimed = SalesLogic.Calculate(input);

        var validation = TransactionLogic.ValidateSale(SaleContext(hasCustomer: false), input, claimed);

        var error = Assert.Single(validation.Errors);
        Assert.Equal(ErrorCode.CustomerRequired, error.Code);
    }

    // 残高不足と販売停止商品は受理して警告
    [Fact]
    public void ValidateSaleWarnings()
    {
        var input = SalesExample.Input();
        var claimed = SalesLogic.Calculate(input);
        var context = SaleContext(
            products: [Product(SalesExample.CameraProduct, isActive: false), Product(SalesExample.SdCardProduct), Product(SalesExample.DeliveryProduct)],
            pointBalance: 1000);

        var validation = TransactionLogic.ValidateSale(context, input, claimed);

        Assert.True(validation.IsValid);
        Assert.Equal(2, validation.Warnings.Count);
        Assert.Contains(validation.Warnings, static x => (x.Code == WarningCode.ProductInactive) && (x.LineId == SalesExample.CameraLine));
        Assert.Contains(validation.Warnings, static x => x.Code == WarningCode.PointBalanceNegative);
    }

    // 締め済みの営業日に届いた販売・返品は受理して警告
    [Fact]
    public void ValidateDayAlreadyClosed()
    {
        // Arrange
        var saleInput = SalesExample.Input();
        var saleClaimed = SalesLogic.Calculate(saleInput);
        var returnInput = ReturnInput(1m);
        var returnClaimed = ReturnLogic.Calculate(returnInput);

        // Act
        var sale = TransactionLogic.ValidateSale(SaleContext(dayClosed: true), saleInput, saleClaimed);
        var returned = TransactionLogic.ValidateReturn(ReturnContext(dayClosed: true), returnInput, returnClaimed);

        // Assert
        Assert.True(sale.IsValid);
        Assert.Equal(WarningCode.DayAlreadyClosed, Assert.Single(sale.Warnings).Code);
        Assert.True(returned.IsValid);
        Assert.Equal(WarningCode.DayAlreadyClosed, Assert.Single(returned.Warnings).Code);
    }

    // 受注から会計するときは、受注が引き渡し待ちであること
    [Fact]
    public void ValidateSaleOrder()
    {
        // Arrange
        var input = SalesExample.Input();
        var claimed = SalesLogic.Calculate(input);
        var orderId = Guid.NewGuid();

        // Act
        var ready = TransactionLogic.ValidateSale(SaleContext(order: new OrderFact { Id = orderId, StoreId = StoreId, Status = OrderStatus.Arrived }, orderId: orderId), input, claimed);
        var notReady = TransactionLogic.ValidateSale(SaleContext(order: new OrderFact { Id = orderId, StoreId = StoreId, Status = OrderStatus.Ordered }, orderId: orderId), input, claimed);
        var missing = TransactionLogic.ValidateSale(SaleContext(orderId: orderId), input, claimed);

        // Assert
        Assert.True(ready.IsValid);
        Assert.Equal(ErrorCode.OrderNotReady, Assert.Single(notReady.Errors).Code);
        Assert.Equal(ErrorCode.OrderNotFound, Assert.Single(missing.Errors).Code);
    }

    // ------------------------------------------------------------
    // Return
    // ------------------------------------------------------------

    [Fact]
    public void ValidateReturnAccepts()
    {
        var input = ReturnInput(1m);
        var claimed = ReturnLogic.Calculate(input);

        var validation = TransactionLogic.ValidateReturn(ReturnContext(), input, claimed);

        Assert.True(validation.IsValid);
        Assert.NotNull(validation.Expected);
    }

    [Fact]
    public void ValidateReturnOriginal()
    {
        var input = ReturnInput(1m);
        var claimed = ReturnLogic.Calculate(input);

        var notFound = TransactionLogic.ValidateReturn(ReturnContext(originalMissing: true), input, claimed);
        Assert.Contains(notFound.Errors, static x => x.Code == ErrorCode.OriginalNotFound);

        var voided = TransactionLogic.ValidateReturn(ReturnContext(original: Original(TransactionType.Sale, TransactionStatus.Voided)), input, claimed);
        Assert.Contains(voided.Errors, static x => x.Code == ErrorCode.OriginalNotReturnable);

        var returnOfReturn = TransactionLogic.ValidateReturn(ReturnContext(original: Original(TransactionType.Return, TransactionStatus.Completed)), input, claimed);
        Assert.Contains(returnOfReturn.Errors, static x => x.Code == ErrorCode.OriginalNotReturnable);
    }

    // 返品数量 ≤ 元数量 − 返品済み数量
    [Fact]
    public void ValidateReturnQuantityExceeded()
    {
        var input = ReturnInput(3m);
        var claimed = ReturnLogic.Calculate(input);

        var validation = TransactionLogic.ValidateReturn(ReturnContext(), input, claimed);

        var error = Assert.Single(validation.Errors);
        Assert.Equal(ErrorCode.ReturnQuantityExceeded, error.Code);
        Assert.Equal(ReturnLineId, error.LineId);
        Assert.Null(validation.Expected);

        var originals = SalesExample.OriginalLines();
        originals[1] = originals[1] with { ReturnedQuantity = 1m };
        var exceededByReturned = TransactionLogic.ValidateReturn(ReturnContext(), ReturnInput(2m) with { OriginalLines = originals }, claimed);
        Assert.Contains(exceededByReturned.Errors, static x => x.Code == ErrorCode.ReturnQuantityExceeded);
    }

    [Fact]
    public void ValidateReturnUnknownOriginalLine()
    {
        var input = ReturnInput(1m) with
        {
            Lines = [new ReturnInputLine { Id = ReturnLineId, LineNo = 1, OriginalLineId = Guid.NewGuid(), Quantity = 1m }]
        };

        var validation = TransactionLogic.ValidateReturn(ReturnContext(), input, ReturnLogic.Calculate(ReturnInput(1m)));

        var error = Assert.Single(validation.Errors);
        Assert.Equal(ErrorCode.ValidationError, error.Code);
        Assert.Null(validation.Expected);
    }

    // 返金は Σ amount = total、預り = 返金額、ポイント返還 = −pointsRedeemed
    [Fact]
    public void ValidateReturnPaymentMismatch()
    {
        var cashOnly = ReturnInput(1m) with { Payments = [SalesExample.Cash(1976m)] };
        var validation = TransactionLogic.ValidateReturn(ReturnContext(), cashOnly, ReturnLogic.Calculate(cashOnly));
        var error = Assert.Single(validation.Errors);
        Assert.Equal(ErrorCode.PaymentMismatch, error.Code);

        var withChange = ReturnInput(1m) with
        {
            Payments =
            [
                new SalesInputPayment { Id = SalesExample.PointsPayment, Kind = PaymentKind.Points, Amount = 123m, TenderedAmount = 123m },
                SalesExample.Cash(1853m, 2000m)
            ]
        };
        validation = TransactionLogic.ValidateReturn(ReturnContext(), withChange, ReturnLogic.Calculate(withChange));
        Assert.Contains(validation.Errors, static x => x.Code == ErrorCode.PaymentMismatch);
    }

    [Fact]
    public void ValidateReturnCustomerRequired()
    {
        var input = ReturnInput(1m);

        var validation = TransactionLogic.ValidateReturn(ReturnContext(hasCustomer: false), input, ReturnLogic.Calculate(input));

        var error = Assert.Single(validation.Errors);
        Assert.Equal(ErrorCode.CustomerRequired, error.Code);
    }

    // ------------------------------------------------------------
    // Void
    // ------------------------------------------------------------

    [Fact]
    public void ValidateVoid()
    {
        var valid = TransactionLogic.ValidateVoid(new VoidContext { Transaction = Transaction(), ShiftStatus = ShiftStatus.Open });
        Assert.True(valid.IsValid);

        var notFound = TransactionLogic.ValidateVoid(new VoidContext { Transaction = null, ShiftStatus = ShiftStatus.Open });
        Assert.Contains(notFound.Errors, static x => x.Code == ErrorCode.NotFound);

        var voided = TransactionLogic.ValidateVoid(new VoidContext { Transaction = Transaction(status: TransactionStatus.Voided), ShiftStatus = ShiftStatus.Open });
        Assert.Contains(voided.Errors, static x => x.Code == ErrorCode.ValidationError);

        var closed = TransactionLogic.ValidateVoid(new VoidContext { Transaction = Transaction(), ShiftStatus = ShiftStatus.Closed });
        Assert.Contains(closed.Errors, static x => x.Code == ErrorCode.ShiftClosed);

        var hasReturns = TransactionLogic.ValidateVoid(new VoidContext { Transaction = Transaction(hasReturns: true), ShiftStatus = ShiftStatus.Open });
        Assert.Contains(hasReturns.Errors, static x => x.Code == ErrorCode.HasReturns);

        var returnWithFlag = TransactionLogic.ValidateVoid(new VoidContext { Transaction = Transaction(TransactionType.Return, hasReturns: true), ShiftStatus = ShiftStatus.Open });
        Assert.True(returnWithFlag.IsValid);

        var dayClosed = TransactionLogic.ValidateVoid(new VoidContext { Transaction = Transaction(), ShiftStatus = ShiftStatus.Open, DayClosed = true });
        Assert.Equal(ErrorCode.DayClosed, Assert.Single(dayClosed.Errors).Code);
    }

    // ------------------------------------------------------------
    // Helper
    // ------------------------------------------------------------

    private static ShiftFact Shift(ShiftStatus status = ShiftStatus.Open, Guid? terminalId = null) => new()
    {
        Id = ShiftId,
        Status = status,
        TerminalId = terminalId ?? TerminalId
    };

    private static ProductFact Product(Guid id, bool allowsPriceOverride = false, bool isActive = true) => new()
    {
        Id = id,
        AllowsPriceOverride = allowsPriceOverride,
        IsActive = isActive
    };

    private static SaleContext SaleContext(ShiftFact? shift = null, bool shiftMissing = false, bool receiptNoInUse = false, IReadOnlyList<ProductFact>? products = null, bool hasCustomer = true, int? pointBalance = 6000, bool dayClosed = false, OrderFact? order = null, Guid? orderId = null)
    {
        products ??= [Product(SalesExample.CameraProduct), Product(SalesExample.SdCardProduct), Product(SalesExample.DeliveryProduct)];
        return new SaleContext
        {
            TerminalId = TerminalId,
            Shift = shiftMissing ? null : (shift ?? Shift()),
            ReceiptNoInUse = receiptNoInUse,
            Products = products.ToDictionary(static x => x.Id),
            HasCustomer = hasCustomer,
            CustomerPointBalance = hasCustomer ? pointBalance : null,
            DayClosed = dayClosed,
            StoreId = StoreId,
            OrderId = orderId,
            Order = order
        };
    }

    private static OriginalTransactionFact Original(TransactionType type, TransactionStatus status) => new()
    {
        Id = OriginalTransactionId,
        Type = type,
        Status = status
    };

    private static ReturnContext ReturnContext(OriginalTransactionFact? original = null, bool originalMissing = false, bool hasCustomer = true, bool dayClosed = false) => new()
    {
        TerminalId = TerminalId,
        Shift = Shift(),
        Original = originalMissing ? null : (original ?? Original(TransactionType.Sale, TransactionStatus.Completed)),
        HasCustomer = hasCustomer,
        DayClosed = dayClosed
    };

    // SD カードを quantity 個返品 (の元取引)
    private static ReturnInput ReturnInput(decimal quantity) => new()
    {
        TaxRounding = TaxRounding.Floor,
        OriginalLines = SalesExample.OriginalLines(),
        Lines = [new ReturnInputLine { Id = ReturnLineId, LineNo = 1, OriginalLineId = SalesExample.SdCardLine, Quantity = quantity }],
        Payments =
        [
            new SalesInputPayment { Id = SalesExample.PointsPayment, Kind = PaymentKind.Points, Amount = 123m, TenderedAmount = 123m },
            SalesExample.Cash(1853m)
        ]
    };

    private static TransactionFact Transaction(TransactionType type = TransactionType.Sale, TransactionStatus status = TransactionStatus.Completed, bool hasReturns = false) => new()
    {
        Id = OriginalTransactionId,
        Type = type,
        Status = status,
        HasReturns = hasReturns
    };
}
