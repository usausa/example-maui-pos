namespace Pos.Domain.Enums;

// 応答の errorCode (ToCode() で UPPER_SNAKE_CASE)
public enum ErrorCode
{
    ValidationError,
    NotFound,
    DuplicateIdMismatch,
    VersionMismatch,
    DuplicateCode,
    TerminalHasOpenShift,
    ShiftNotFound,
    ShiftClosed,
    ShiftTerminalMismatch,
    DuplicateReceiptNo,
    ProductNotFound,
    PriceOverrideNotAllowed,
    CalculationMismatch,
    PaymentMismatch,
    CustomerRequired,
    OriginalNotFound,
    OriginalNotReturnable,
    ReturnQuantityExceeded,
    HasReturns,
    InUse,
    ShiftStillOpen,
    AlreadyClosed,
    DayClosed,
    OrderNotFound,
    OrderNotReady,
    OrderStatusInvalid,
    InventoryReceiptStatusInvalid,
    InventoryTransferStatusInvalid
}
