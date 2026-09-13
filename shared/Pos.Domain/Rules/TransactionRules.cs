namespace Pos.Domain.Rules;

using Pos.Domain.Sales;

// api-design §3.12 の業務ルール。DB は見ず、必要な事実は Context で受け取る
public static class TransactionRules
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
            errors.Add(new RuleError(ErrorCode.DuplicateReceiptNo, "レシート番号が重複しています"));
        }

        foreach (var line in input.Lines)
        {
            if (!context.Products.TryGetValue(line.ProductId, out var product))
            {
                errors.Add(new RuleError(ErrorCode.ProductNotFound, "商品が見つかりません", line.Id));
                continue;
            }

            if (!product.AllowsPriceOverride && (line.UnitPrice != line.ListPrice))
            {
                errors.Add(new RuleError(ErrorCode.PriceOverrideNotAllowed, "売価を変更できない商品です", line.Id));
            }

            if (!product.IsActive)
            {
                warnings.Add(new RuleWarning(WarningCode.ProductInactive, "販売停止中の商品です", line.Id));
            }
        }

        if (!ValidateSalesInput(input, errors))
        {
            return new TransactionValidation { Errors = errors, Warnings = warnings };
        }

        var expected = SalesCalculator.Calculate(input);
        if (!SalesResultComparer.Matches(expected, claimed))
        {
            errors.Add(new RuleError(ErrorCode.CalculationMismatch, "計算結果が一致しません"));
        }

        ValidateSalePayments(input.Payments, expected, errors);

        if (((expected.PointsEarned > 0) || (expected.PointsRedeemed > 0)) && !context.HasCustomer)
        {
            errors.Add(new RuleError(ErrorCode.CustomerRequired, "ポイントの付与・利用には会員の指定が必要です"));
        }

        if (context.HasCustomer && (context.CustomerPointBalance is { } balance) && (balance - expected.PointsRedeemed < 0))
        {
            warnings.Add(new RuleWarning(WarningCode.PointBalanceNegative, "ポイント残高が不足しています"));
        }

        return new TransactionValidation { Errors = errors, Warnings = warnings, Expected = expected };
    }

    // 計算できる入力かを検証する (api-design §4.1 / §4.2 の前提)
    private static bool ValidateSalesInput(SalesInput input, List<RuleError> errors)
    {
        var valid = true;

        if (input.Lines.Count == 0)
        {
            errors.Add(new RuleError(ErrorCode.ValidationError, "明細がありません"));
            return false;
        }

        var amounts = new Dictionary<Guid, decimal>(input.Lines.Count);
        foreach (var line in input.Lines)
        {
            if (!amounts.TryAdd(line.Id, Math.Floor(line.UnitPrice * line.Quantity)))
            {
                errors.Add(new RuleError(ErrorCode.ValidationError, "明細 ID が重複しています", line.Id));
                valid = false;
            }

            if (line.Quantity <= 0)
            {
                errors.Add(new RuleError(ErrorCode.ValidationError, "数量は正の数を指定してください", line.Id));
                valid = false;
            }

            if (line.UnitPrice < 0)
            {
                errors.Add(new RuleError(ErrorCode.ValidationError, "単価は 0 以上を指定してください", line.Id));
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
                errors.Add(new RuleError(ErrorCode.ValidationError, "値引の値が不正です", discount.LineId));
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
                errors.Add(new RuleError(ErrorCode.ValidationError, "値引の対象明細が存在しません", discount.LineId));
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
                errors.Add(new RuleError(ErrorCode.ValidationError, "明細値引が明細金額を超えています", line.Id));
                valid = false;
            }

            baseTotal += amount - discountAmount;
        }

        if (valid && (transactionDiscount + Math.Floor(baseTotal * transactionPercent) > baseTotal))
        {
            errors.Add(new RuleError(ErrorCode.ValidationError, "取引値引が値引後の合計を超えています"));
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
                errors.Add(new RuleError(ErrorCode.PaymentMismatch, "支払額が不正です"));
            }
            else if (payment.TenderedAmount < payment.Amount)
            {
                errors.Add(new RuleError(ErrorCode.PaymentMismatch, "預り額が支払額より少なくなっています"));
            }
            else if (!payment.AllowsChange && (payment.TenderedAmount != payment.Amount))
            {
                errors.Add(new RuleError(ErrorCode.PaymentMismatch, "釣銭を出せない支払方法です"));
            }
        }

        if (amountTotal != expected.Total)
        {
            errors.Add(new RuleError(ErrorCode.PaymentMismatch, "支払合計が会計金額と一致しません"));
        }

        if (expected.ChangeAmount < 0)
        {
            errors.Add(new RuleError(ErrorCode.PaymentMismatch, "預り合計が会計金額に足りません"));
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
            errors.Add(new RuleError(ErrorCode.DuplicateReceiptNo, "レシート番号が重複しています"));
        }

        if (context.Original is null)
        {
            errors.Add(new RuleError(ErrorCode.OriginalNotFound, "元取引が見つかりません"));
        }
        else if ((context.Original.Type != TransactionType.Sale) || (context.Original.Status != TransactionStatus.Completed))
        {
            errors.Add(new RuleError(ErrorCode.OriginalNotReturnable, "返品できない取引です"));
        }

        if (!ValidateReturnInput(input, errors))
        {
            return new TransactionValidation { Errors = errors, Warnings = warnings };
        }

        var expected = ReturnCalculator.Calculate(input);
        if (!SalesResultComparer.Matches(expected, claimed))
        {
            errors.Add(new RuleError(ErrorCode.CalculationMismatch, "計算結果が一致しません"));
        }

        ValidateReturnPayments(input.Payments, expected, errors);

        if (((expected.PointsEarned != 0) || (expected.PointsRedeemed != 0)) && !context.HasCustomer)
        {
            errors.Add(new RuleError(ErrorCode.CustomerRequired, "ポイントの取消・返還には会員の指定が必要です"));
        }

        return new TransactionValidation { Errors = errors, Warnings = warnings, Expected = expected };
    }

    private static bool ValidateReturnInput(ReturnInput input, List<RuleError> errors)
    {
        var valid = true;

        if (input.Lines.Count == 0)
        {
            errors.Add(new RuleError(ErrorCode.ValidationError, "明細がありません"));
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
                errors.Add(new RuleError(ErrorCode.ValidationError, "元取引の明細ではありません", line.Id));
                valid = false;
                continue;
            }

            if (line.Quantity <= 0)
            {
                errors.Add(new RuleError(ErrorCode.ValidationError, "数量は正の数を指定してください", line.Id));
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
                errors.Add(new RuleError(ErrorCode.ReturnQuantityExceeded, "返品数量が返品可能な数量を超えています", line.Id));
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
                errors.Add(new RuleError(ErrorCode.PaymentMismatch, "返金額が不正です"));
            }
            else if (payment.TenderedAmount != payment.Amount)
            {
                errors.Add(new RuleError(ErrorCode.PaymentMismatch, "返金では預り額と返金額を同じにしてください"));
            }
        }

        if (amountTotal != expected.Total)
        {
            errors.Add(new RuleError(ErrorCode.PaymentMismatch, "返金合計が返品金額と一致しません"));
        }

        if (pointsTotal != -expected.PointsRedeemed)
        {
            errors.Add(new RuleError(ErrorCode.PaymentMismatch, "ポイントの返還額が利用ポイントと一致しません"));
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
            errors.Add(new RuleError(ErrorCode.NotFound, "取引が見つかりません"));
            return new TransactionValidation { Errors = errors, Warnings = [] };
        }

        if (context.Transaction.Status != TransactionStatus.Completed)
        {
            errors.Add(new RuleError(ErrorCode.ValidationError, "取消済みの取引です"));
        }

        if (context.ShiftStatus is null)
        {
            errors.Add(new RuleError(ErrorCode.ShiftNotFound, "シフトが見つかりません"));
        }
        else if (context.ShiftStatus != ShiftStatus.Open)
        {
            errors.Add(new RuleError(ErrorCode.ShiftClosed, "精算済みのシフトの取引は取消できません"));
        }

        if ((context.Transaction.Type == TransactionType.Sale) && context.Transaction.HasReturns)
        {
            errors.Add(new RuleError(ErrorCode.HasReturns, "返品済みの取引は取消できません"));
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
            errors.Add(new RuleError(ErrorCode.ShiftNotFound, "シフトが見つかりません"));
        }
        else if (shift.Status != ShiftStatus.Open)
        {
            errors.Add(new RuleError(ErrorCode.ShiftClosed, "シフトが精算済みです"));
        }
        else if (shift.TerminalId != terminalId)
        {
            errors.Add(new RuleError(ErrorCode.ShiftTerminalMismatch, "シフトの端末が一致しません"));
        }
    }
}
