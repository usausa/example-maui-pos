namespace Pos.Domain.Logic;

// 取引の業務ルール。DB は見ず、必要な事実は Context で受け取る
public static class TransactionLogic
{
    // ------------------------------------------------------------
    // Sale
    // ------------------------------------------------------------

    public static TransactionValidation ValidateSale(SaleContext context, SalesInput input, SalesResult claimed)
    {
        var errors = new List<RuleError>();
        var warnings = new List<RuleWarning>();

        ValidateShift(context.Shift, context.TerminalId, errors);
        if (context.ReceiptNoInUse)
        {
            errors.Add(new RuleError(ErrorCode.DuplicateReceiptNo, RuleReason.DuplicateReceiptNo));
        }

        foreach (var line in input.Lines)
        {
            if (!context.Products.TryGetValue(line.ProductId, out var product))
            {
                errors.Add(new RuleError(ErrorCode.ProductNotFound, RuleReason.ProductNotFound, line.Id));
                continue;
            }

            if (!product.AllowsPriceOverride && (line.UnitPrice != line.ListPrice))
            {
                errors.Add(new RuleError(ErrorCode.PriceOverrideNotAllowed, RuleReason.PriceOverrideNotAllowed, line.Id));
            }

            if (!product.IsActive)
            {
                warnings.Add(new RuleWarning(WarningCode.ProductInactive, line.Id));
            }
        }

        if (!ValidateSalesInput(input, errors))
        {
            return new TransactionValidation { Errors = errors, Warnings = warnings };
        }

        var expected = SalesLogic.Calculate(input);
        if (!SalesLogic.Matches(expected, claimed))
        {
            errors.Add(new RuleError(ErrorCode.CalculationMismatch, RuleReason.CalculationMismatch));
        }

        ValidateSalePayments(input.Payments, expected, errors);

        if (((expected.PointsEarned > 0) || (expected.PointsRedeemed > 0)) && !context.HasCustomer)
        {
            errors.Add(new RuleError(ErrorCode.CustomerRequired, RuleReason.CustomerRequiredForPoints));
        }

        if (context.HasCustomer && (context.CustomerPointBalance is { } balance) && (balance - expected.PointsRedeemed < 0))
        {
            warnings.Add(new RuleWarning(WarningCode.PointBalanceNegative));
        }

        return new TransactionValidation { Errors = errors, Warnings = warnings, Expected = expected };
    }

    // 計算できる入力か。端末のカート検証や計算 API の事前チェックにも使う
    public static IReadOnlyList<RuleError> ValidateInput(SalesInput input)
    {
        var errors = new List<RuleError>();
        ValidateSalesInput(input, errors);
        return errors;
    }

    public static IReadOnlyList<RuleError> ValidateInput(ReturnInput input)
    {
        var errors = new List<RuleError>();
        ValidateReturnInput(input, errors);
        return errors;
    }

    private static bool ValidateSalesInput(SalesInput input, List<RuleError> errors)
    {
        var valid = true;

        if (input.Lines.Count == 0)
        {
            errors.Add(new RuleError(ErrorCode.ValidationError, RuleReason.NoLines));
            return false;
        }

        var amounts = new Dictionary<Guid, decimal>(input.Lines.Count);
        foreach (var line in input.Lines)
        {
            if (!amounts.TryAdd(line.Id, Math.Floor(line.UnitPrice * line.Quantity)))
            {
                errors.Add(new RuleError(ErrorCode.ValidationError, RuleReason.DuplicateLineId, line.Id));
                valid = false;
            }

            if (line.Quantity <= 0)
            {
                errors.Add(new RuleError(ErrorCode.ValidationError, RuleReason.QuantityNotPositive, line.Id));
                valid = false;
            }

            if (line.UnitPrice < 0)
            {
                errors.Add(new RuleError(ErrorCode.ValidationError, RuleReason.UnitPriceNegative, line.Id));
                valid = false;
            }
        }

        var lineDiscounts = new Dictionary<Guid, decimal>(input.Lines.Count);
        var transactionDiscount = 0m;
        var transactionPercent = 0m;
        foreach (var discount in input.Discounts)
        {
            if ((discount.Value < 0) || ((discount.Type == DiscountType.Percent) && (discount.Value > 1)))
            {
                errors.Add(new RuleError(ErrorCode.ValidationError, RuleReason.DiscountValueInvalid, discount.LineId));
                valid = false;
                continue;
            }

            if (discount.LineId is null)
            {
                if (discount.Type == DiscountType.Amount)
                {
                    transactionDiscount += discount.Value;
                }
                else
                {
                    transactionPercent += discount.Value;
                }

                continue;
            }

            if (!amounts.TryGetValue(discount.LineId.Value, out var amount))
            {
                errors.Add(new RuleError(ErrorCode.ValidationError, RuleReason.DiscountLineNotFound, discount.LineId));
                valid = false;
                continue;
            }

            var discountAmount = discount.Type == DiscountType.Amount ? discount.Value : Math.Floor(amount * discount.Value);
            lineDiscounts[discount.LineId.Value] = lineDiscounts.GetValueOrDefault(discount.LineId.Value) + discountAmount;
        }

        if (!valid)
        {
            return false;
        }

        var baseTotal = 0m;
        foreach (var line in input.Lines)
        {
            var amount = amounts[line.Id];
            var discountAmount = lineDiscounts.GetValueOrDefault(line.Id);
            if (discountAmount > amount)
            {
                errors.Add(new RuleError(ErrorCode.ValidationError, RuleReason.LineDiscountExceeds, line.Id));
                valid = false;
            }

            baseTotal += amount - discountAmount;
        }

        if (valid && (transactionDiscount + Math.Floor(baseTotal * transactionPercent) > baseTotal))
        {
            errors.Add(new RuleError(ErrorCode.ValidationError, RuleReason.TransactionDiscountExceeds));
            valid = false;
        }

        return valid;
    }

    private static void ValidateSalePayments(IEnumerable<SalesInputPayment> payments, SalesResult expected, List<RuleError> errors)
    {
        var amountTotal = 0m;
        foreach (var payment in payments)
        {
            amountTotal += payment.Amount;

            if (payment.Amount < 0)
            {
                errors.Add(new RuleError(ErrorCode.PaymentMismatch, RuleReason.PaymentAmountInvalid));
            }
            else if (payment.TenderedAmount < payment.Amount)
            {
                errors.Add(new RuleError(ErrorCode.PaymentMismatch, RuleReason.TenderedLessThanAmount));
            }
            else if (!payment.AllowsChange && (payment.TenderedAmount != payment.Amount))
            {
                errors.Add(new RuleError(ErrorCode.PaymentMismatch, RuleReason.ChangeNotAllowed));
            }
        }

        if (amountTotal != expected.Total)
        {
            errors.Add(new RuleError(ErrorCode.PaymentMismatch, RuleReason.PaymentTotalMismatch));
        }

        if (expected.ChangeAmount < 0)
        {
            errors.Add(new RuleError(ErrorCode.PaymentMismatch, RuleReason.TenderedShort));
        }
    }

    // ------------------------------------------------------------
    // Return
    // ------------------------------------------------------------

    public static TransactionValidation ValidateReturn(ReturnContext context, ReturnInput input, SalesResult claimed)
    {
        var errors = new List<RuleError>();
        var warnings = new List<RuleWarning>();

        ValidateShift(context.Shift, context.TerminalId, errors);
        if (context.ReceiptNoInUse)
        {
            errors.Add(new RuleError(ErrorCode.DuplicateReceiptNo, RuleReason.DuplicateReceiptNo));
        }

        if (context.Original is null)
        {
            errors.Add(new RuleError(ErrorCode.OriginalNotFound, RuleReason.OriginalNotFound));
        }
        else if ((context.Original.Type != TransactionType.Sale) || (context.Original.Status != TransactionStatus.Completed))
        {
            errors.Add(new RuleError(ErrorCode.OriginalNotReturnable, RuleReason.OriginalNotReturnable));
        }

        if (!ValidateReturnInput(input, errors))
        {
            return new TransactionValidation { Errors = errors, Warnings = warnings };
        }

        var expected = ReturnLogic.Calculate(input);
        if (!SalesLogic.Matches(expected, claimed))
        {
            errors.Add(new RuleError(ErrorCode.CalculationMismatch, RuleReason.CalculationMismatch));
        }

        ValidateReturnPayments(input.Payments, expected, errors);

        if (((expected.PointsEarned != 0) || (expected.PointsRedeemed != 0)) && !context.HasCustomer)
        {
            errors.Add(new RuleError(ErrorCode.CustomerRequired, RuleReason.CustomerRequiredForPointRefund));
        }

        return new TransactionValidation { Errors = errors, Warnings = warnings, Expected = expected };
    }

    private static bool ValidateReturnInput(ReturnInput input, List<RuleError> errors)
    {
        var valid = true;

        if (input.Lines.Count == 0)
        {
            errors.Add(new RuleError(ErrorCode.ValidationError, RuleReason.NoLines));
            return false;
        }

        var originals = new Dictionary<Guid, ReturnOriginalLine>(input.OriginalLines.Count);
        foreach (var original in input.OriginalLines)
        {
            originals[original.Id] = original;
        }

        var quantities = new Dictionary<Guid, decimal>(input.Lines.Count);
        foreach (var line in input.Lines)
        {
            if (!originals.ContainsKey(line.OriginalLineId))
            {
                errors.Add(new RuleError(ErrorCode.ValidationError, RuleReason.LineNotInOriginal, line.Id));
                valid = false;
                continue;
            }

            if (line.Quantity <= 0)
            {
                errors.Add(new RuleError(ErrorCode.ValidationError, RuleReason.QuantityNotPositive, line.Id));
                valid = false;
                continue;
            }

            quantities[line.OriginalLineId] = quantities.GetValueOrDefault(line.OriginalLineId) + line.Quantity;
        }

        foreach (var line in input.Lines)
        {
            if (!quantities.TryGetValue(line.OriginalLineId, out var quantity))
            {
                continue;
            }

            var original = originals[line.OriginalLineId];
            if (quantity > original.Quantity - original.ReturnedQuantity)
            {
                errors.Add(new RuleError(ErrorCode.ReturnQuantityExceeded, RuleReason.ReturnQuantityExceeded, line.Id));
                valid = false;
            }
        }

        return valid;
    }

    private static void ValidateReturnPayments(IEnumerable<SalesInputPayment> payments, SalesResult expected, List<RuleError> errors)
    {
        var amountTotal = 0m;
        var pointsTotal = 0m;
        foreach (var payment in payments)
        {
            amountTotal += payment.Amount;
            if (payment.Kind == PaymentKind.Points)
            {
                pointsTotal += payment.Amount;
            }

            if (payment.Amount < 0)
            {
                errors.Add(new RuleError(ErrorCode.PaymentMismatch, RuleReason.RefundAmountInvalid));
            }
            else if (payment.TenderedAmount != payment.Amount)
            {
                errors.Add(new RuleError(ErrorCode.PaymentMismatch, RuleReason.RefundTenderedMismatch));
            }
        }

        if (amountTotal != expected.Total)
        {
            errors.Add(new RuleError(ErrorCode.PaymentMismatch, RuleReason.RefundTotalMismatch));
        }

        if (pointsTotal != -expected.PointsRedeemed)
        {
            errors.Add(new RuleError(ErrorCode.PaymentMismatch, RuleReason.PointRefundMismatch));
        }
    }

    // ------------------------------------------------------------
    // Void
    // ------------------------------------------------------------

    public static TransactionValidation ValidateVoid(VoidContext context)
    {
        var errors = new List<RuleError>();

        if (context.Transaction is null)
        {
            errors.Add(new RuleError(ErrorCode.NotFound, RuleReason.TransactionNotFound));
            return new TransactionValidation { Errors = errors, Warnings = [] };
        }

        if (context.Transaction.Status != TransactionStatus.Completed)
        {
            errors.Add(new RuleError(ErrorCode.ValidationError, RuleReason.AlreadyVoided));
        }

        if (context.ShiftStatus is null)
        {
            errors.Add(new RuleError(ErrorCode.ShiftNotFound, RuleReason.ShiftNotFound));
        }
        else if (context.ShiftStatus != ShiftStatus.Open)
        {
            errors.Add(new RuleError(ErrorCode.ShiftClosed, RuleReason.ShiftClosedForVoid));
        }

        if ((context.Transaction.Type == TransactionType.Sale) && context.Transaction.HasReturns)
        {
            errors.Add(new RuleError(ErrorCode.HasReturns, RuleReason.HasReturns));
        }

        return new TransactionValidation { Errors = errors, Warnings = [] };
    }

    // ------------------------------------------------------------
    // Helper
    // ------------------------------------------------------------

    private static void ValidateShift(ShiftFact? shift, Guid terminalId, List<RuleError> errors)
    {
        if (shift is null)
        {
            errors.Add(new RuleError(ErrorCode.ShiftNotFound, RuleReason.ShiftNotFound));
        }
        else if (shift.Status != ShiftStatus.Open)
        {
            errors.Add(new RuleError(ErrorCode.ShiftClosed, RuleReason.ShiftClosed));
        }
        else if (shift.TerminalId != terminalId)
        {
            errors.Add(new RuleError(ErrorCode.ShiftTerminalMismatch, RuleReason.ShiftTerminalMismatch));
        }
    }
}
