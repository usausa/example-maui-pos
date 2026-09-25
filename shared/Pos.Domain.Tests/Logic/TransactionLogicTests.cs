namespace Pos.Domain.Logic;

public sealed class TransactionLogicTests
{
    private static readonly Guid StoreId = new("00000000-0000-0000-0001-000000000001");

    private static readonly Guid TerminalId = new("00000000-0000-0000-0006-000000000001");

    private static readonly Guid OtherTerminalId = new("00000000-0000-0000-0006-000000000002");

    private static readonly Guid ShiftId = new("00000000-0000-0000-0007-000000000001");

    private static readonly Guid OriginalTransactionId = new("00000000-0000-0000-0008-000000000001");

    private static readonly Guid ReturnLineId = new("00000000-0000-0000-0005-000000000001");

    private static readonly Guid OtherStoreId = new("00000000-0000-0000-0001-000000000002");

    private static readonly Guid CashierId = new("00000000-0000-0000-0003-000000000003");

    private static readonly Guid ManagerId = new("00000000-0000-0000-0003-000000000002");

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

    // 受注の前受金は全額を会計で充てる (受注のない会計や、前受金と違う額は不可)
    [Fact]
    public void ValidateSaleDeposit()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        var payments = SalesExample.Payments();
        payments[1] = new SalesInputPayment { Id = SalesExample.CardPayment, Kind = PaymentKind.Deposit, Amount = 50000m, TenderedAmount = 50000m };
        var input = SalesExample.Input() with { Payments = payments };
        var claimed = SalesLogic.Calculate(input);
        var withoutDeposit = SalesExample.Input();
        var withoutDepositClaimed = SalesLogic.Calculate(withoutDeposit);

        // Act
        var applied = TransactionLogic.ValidateSale(SaleContext(order: Order(orderId, 50000m), orderId: orderId), input, claimed);
        var differs = TransactionLogic.ValidateSale(SaleContext(order: Order(orderId, 30000m), orderId: orderId), input, claimed);
        var noOrder = TransactionLogic.ValidateSale(SaleContext(), input, claimed);
        var notApplied = TransactionLogic.ValidateSale(SaleContext(order: Order(orderId, 50000m), orderId: orderId), withoutDeposit, withoutDepositClaimed);

        // Assert
        Assert.True(applied.IsValid);
        Assert.Equal((ErrorCode.PaymentMismatch, RuleReason.DepositMismatch), (Assert.Single(differs.Errors).Code, differs.Errors[0].Reason));
        Assert.Equal(RuleReason.DepositMismatch, Assert.Single(noOrder.Errors).Reason);
        Assert.Equal(RuleReason.DepositMismatch, Assert.Single(notApplied.Errors).Reason);
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

    // 前受金は受注の会計で充てる支払なので、返品の返金には使えない
    [Fact]
    public void ValidateReturnRejectsDeposit()
    {
        // Arrange
        var input = ReturnInput(1m) with
        {
            Payments =
            [
                new SalesInputPayment { Id = SalesExample.PointsPayment, Kind = PaymentKind.Points, Amount = 123m, TenderedAmount = 123m },
                new SalesInputPayment { Id = SalesExample.CashPayment, Kind = PaymentKind.Deposit, Amount = 1853m, TenderedAmount = 1853m }
            ]
        };
        var claimed = ReturnLogic.Calculate(input);

        // Act
        var validation = TransactionLogic.ValidateReturn(ReturnContext(), input, claimed);

        // Assert
        Assert.Equal((ErrorCode.PaymentMismatch, RuleReason.DepositRefundNotAllowed), (Assert.Single(validation.Errors).Code, validation.Errors[0].Reason));
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
        var valid = TransactionLogic.ValidateVoid(VoidContext(Transaction(), ShiftStatus.Open));
        Assert.True(valid.IsValid);

        var notFound = TransactionLogic.ValidateVoid(VoidContext(null, ShiftStatus.Open));
        Assert.Contains(notFound.Errors, static x => x.Code == ErrorCode.NotFound);

        var voided = TransactionLogic.ValidateVoid(VoidContext(Transaction(status: TransactionStatus.Voided), ShiftStatus.Open));
        Assert.Contains(voided.Errors, static x => x.Code == ErrorCode.ValidationError);

        var closed = TransactionLogic.ValidateVoid(VoidContext(Transaction(), ShiftStatus.Closed));
        Assert.Contains(closed.Errors, static x => x.Code == ErrorCode.ShiftClosed);

        var hasReturns = TransactionLogic.ValidateVoid(VoidContext(Transaction(hasReturns: true), ShiftStatus.Open));
        Assert.Contains(hasReturns.Errors, static x => x.Code == ErrorCode.HasReturns);

        var returnWithFlag = TransactionLogic.ValidateVoid(VoidContext(Transaction(TransactionType.Return, hasReturns: true), ShiftStatus.Open));
        Assert.True(returnWithFlag.IsValid);

        var dayClosed = TransactionLogic.ValidateVoid(VoidContext(Transaction(), ShiftStatus.Open, dayClosed: true));
        Assert.Equal(ErrorCode.DayClosed, Assert.Single(dayClosed.Errors).Code);
    }

    // レジ係の取消は店長以上の承認が要る。店長以上は承認なしで取消せる
    [Fact]
    public void ValidateVoidApproval()
    {
        // Arrange
        var cashier = Staff(CashierId, StaffRole.Cashier);
        var manager = Staff(ManagerId, StaffRole.Manager);

        // Act
        var withoutApprover = TransactionLogic.ValidateVoid(VoidContext(Transaction(), ShiftStatus.Open, staff: cashier));
        var cashierApprover = TransactionLogic.ValidateVoid(VoidContext(Transaction(), ShiftStatus.Open, staff: cashier, approver: Staff(CashierId, StaffRole.Cashier)));
        var managerApprover = TransactionLogic.ValidateVoid(VoidContext(Transaction(), ShiftStatus.Open, staff: cashier, approver: manager));
        var byManager = TransactionLogic.ValidateVoid(VoidContext(Transaction(), ShiftStatus.Open, staff: manager));

        // Assert
        Assert.Equal(RuleReason.VoidApprovalRequired, Assert.Single(withoutApprover.Errors).Reason);
        Assert.Equal(ErrorCode.ApprovalRequired, Assert.Single(withoutApprover.Errors).Code);
        Assert.Equal(RuleReason.ApproverNotAllowed, Assert.Single(cashierApprover.Errors).Reason);
        Assert.True(managerApprover.IsValid);
        Assert.True(byManager.IsValid);
    }

    // 担当は有効で、その店舗か本部に所属していること
    [Fact]
    public void ValidateSaleStaff()
    {
        // Arrange
        var input = SalesExample.Input();
        var claimed = SalesLogic.Calculate(input);

        // Act
        var missing = TransactionLogic.ValidateSale(SaleContext(staffMissing: true), input, claimed);
        var otherStore = TransactionLogic.ValidateSale(SaleContext(staff: Staff(CashierId, StaffRole.Cashier, OtherStoreId)), input, claimed);
        var inactive = TransactionLogic.ValidateSale(SaleContext(staff: Staff(CashierId, StaffRole.Cashier, isActive: false)), input, claimed);
        var headquarters = TransactionLogic.ValidateSale(SaleContext(staff: Staff(CashierId, StaffRole.Admin, headquarters: true)), input, claimed);

        // Assert
        Assert.Equal(ErrorCode.StaffInvalid, Assert.Single(missing.Errors).Code);
        Assert.Equal(RuleReason.StaffNotAllowed, Assert.Single(otherStore.Errors).Reason);
        Assert.Equal(RuleReason.StaffNotAllowed, Assert.Single(inactive.Errors).Reason);
        Assert.True(headquarters.IsValid);
    }

    // 承認が必要な値引は、その店舗 (または本部) の店長以上の承認者が要る
    [Fact]
    public void ValidateSaleDiscountApproval()
    {
        // Arrange
        var input = SalesExample.Input();
        var claimed = SalesLogic.Calculate(input);
        var manager = Staff(ManagerId, StaffRole.Manager);

        // Act
        var withoutApprover = TransactionLogic.ValidateSale(SaleContext(approvals: [new DiscountApprovalFact()]), input, claimed);
        var cashierApprover = TransactionLogic.ValidateSale(SaleContext(approvals: [new DiscountApprovalFact { ApproverId = CashierId, Approver = Staff(CashierId, StaffRole.Cashier) }]), input, claimed);
        var unknownApprover = TransactionLogic.ValidateSale(SaleContext(approvals: [new DiscountApprovalFact { ApproverId = ManagerId }]), input, claimed);
        var otherStoreApprover = TransactionLogic.ValidateSale(SaleContext(approvals: [new DiscountApprovalFact { ApproverId = ManagerId, Approver = Staff(ManagerId, StaffRole.Manager, OtherStoreId) }]), input, claimed);
        var approved = TransactionLogic.ValidateSale(SaleContext(approvals: [new DiscountApprovalFact { ApproverId = ManagerId, Approver = manager }]), input, claimed);

        // Assert
        Assert.Equal(RuleReason.DiscountApprovalRequired, Assert.Single(withoutApprover.Errors).Reason);
        Assert.Equal(ErrorCode.ApprovalRequired, Assert.Single(withoutApprover.Errors).Code);
        Assert.Equal(RuleReason.ApproverNotAllowed, Assert.Single(cashierApprover.Errors).Reason);
        Assert.Equal(RuleReason.ApproverNotAllowed, Assert.Single(unknownApprover.Errors).Reason);
        Assert.Equal(RuleReason.ApproverNotAllowed, Assert.Single(otherStoreApprover.Errors).Reason);
        Assert.True(approved.IsValid);
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

    // 既定は自店の有効なスタッフ (headquarters = true なら本部)
    private static StaffFact Staff(Guid id, StaffRole role, Guid? storeId = null, bool headquarters = false, bool isActive = true) => new()
    {
        Id = id,
        Role = role,
        StoreId = headquarters ? null : storeId ?? StoreId,
        IsActive = isActive
    };

    private static SaleContext SaleContext(ShiftFact? shift = null, bool shiftMissing = false, bool receiptNoInUse = false, IReadOnlyList<ProductFact>? products = null, bool hasCustomer = true, int? pointBalance = 6000, bool dayClosed = false, OrderFact? order = null, Guid? orderId = null, StaffFact? staff = null, bool staffMissing = false, IReadOnlyList<DiscountApprovalFact>? approvals = null)
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
            Order = order,
            Staff = staffMissing ? null : (staff ?? Staff(ManagerId, StaffRole.Manager)),
            DiscountApprovals = approvals ?? []
        };
    }

    private static OrderFact Order(Guid id, decimal depositBalance) => new()
    {
        Id = id,
        StoreId = StoreId,
        Status = OrderStatus.Arrived,
        DepositBalance = depositBalance
    };

    private static OriginalTransactionFact Original(TransactionType type, TransactionStatus status) => new()
    {
        Id = OriginalTransactionId,
        Type = type,
        Status = status
    };

    private static ReturnContext ReturnContext(OriginalTransactionFact? original = null, bool originalMissing = false, bool hasCustomer = true, bool dayClosed = false) => new()
    {
        StoreId = StoreId,
        TerminalId = TerminalId,
        Shift = Shift(),
        Original = originalMissing ? null : (original ?? Original(TransactionType.Sale, TransactionStatus.Completed)),
        HasCustomer = hasCustomer,
        DayClosed = dayClosed,
        Staff = Staff(ManagerId, StaffRole.Manager)
    };

    // 取消の担当は既定で自店の店長
    private static VoidContext VoidContext(TransactionFact? transaction, ShiftStatus shiftStatus, bool dayClosed = false, StaffFact? staff = null, StaffFact? approver = null) => new()
    {
        Transaction = transaction,
        ShiftStatus = shiftStatus,
        DayClosed = dayClosed,
        Staff = staff ?? Staff(ManagerId, StaffRole.Manager),
        ApproverId = approver?.Id,
        Approver = approver
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
        StoreId = StoreId,
        Type = type,
        Status = status,
        HasReturns = hasReturns
    };
}
