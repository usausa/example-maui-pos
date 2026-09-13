namespace Pos.Domain.Rules;

public sealed class ErrorCodeTests
{
    // api-design §5 の errorCode 文字列
    [Theory]
    [InlineData(ErrorCode.ValidationError, "VALIDATION_ERROR")]
    [InlineData(ErrorCode.NotFound, "NOT_FOUND")]
    [InlineData(ErrorCode.DuplicateIdMismatch, "DUPLICATE_ID_MISMATCH")]
    [InlineData(ErrorCode.VersionMismatch, "VERSION_MISMATCH")]
    [InlineData(ErrorCode.DuplicateCode, "DUPLICATE_CODE")]
    [InlineData(ErrorCode.TerminalHasOpenShift, "TERMINAL_HAS_OPEN_SHIFT")]
    [InlineData(ErrorCode.ShiftNotFound, "SHIFT_NOT_FOUND")]
    [InlineData(ErrorCode.ShiftClosed, "SHIFT_CLOSED")]
    [InlineData(ErrorCode.ShiftTerminalMismatch, "SHIFT_TERMINAL_MISMATCH")]
    [InlineData(ErrorCode.DuplicateReceiptNo, "DUPLICATE_RECEIPT_NO")]
    [InlineData(ErrorCode.ProductNotFound, "PRODUCT_NOT_FOUND")]
    [InlineData(ErrorCode.PriceOverrideNotAllowed, "PRICE_OVERRIDE_NOT_ALLOWED")]
    [InlineData(ErrorCode.CalculationMismatch, "CALCULATION_MISMATCH")]
    [InlineData(ErrorCode.PaymentMismatch, "PAYMENT_MISMATCH")]
    [InlineData(ErrorCode.CustomerRequired, "CUSTOMER_REQUIRED")]
    [InlineData(ErrorCode.OriginalNotFound, "ORIGINAL_NOT_FOUND")]
    [InlineData(ErrorCode.OriginalNotReturnable, "ORIGINAL_NOT_RETURNABLE")]
    [InlineData(ErrorCode.ReturnQuantityExceeded, "RETURN_QUANTITY_EXCEEDED")]
    [InlineData(ErrorCode.HasReturns, "HAS_RETURNS")]
    [InlineData(ErrorCode.InUse, "IN_USE")]
    public void ErrorCodeToCode(ErrorCode code, string expected)
    {
        Assert.Equal(expected, code.ToCode());
    }

    [Theory]
    [InlineData(WarningCode.PointBalanceNegative, "POINT_BALANCE_NEGATIVE")]
    [InlineData(WarningCode.ProductInactive, "PRODUCT_INACTIVE")]
    [InlineData(WarningCode.InventoryNegative, "INVENTORY_NEGATIVE")]
    public void WarningCodeToCode(WarningCode code, string expected)
    {
        Assert.Equal(expected, code.ToCode());
    }
}
