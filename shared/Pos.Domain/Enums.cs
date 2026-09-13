namespace Pos.Domain;

public enum TransactionType
{
    Sale,
    Return
}

public enum TransactionStatus
{
    Completed,
    Voided
}

public enum ProductKind
{
    Goods,
    Service
}

public enum PaymentKind
{
    Cash,
    Card,
    Qr,
    EMoney,
    Voucher,
    Points,
    Credit,
    Other
}

public enum DiscountType
{
    Amount,
    Percent
}

public enum DiscountScope
{
    Line,
    Transaction
}

public enum TaxKind
{
    Standard,
    Reduced,
    Exempt
}

public enum StaffRole
{
    Cashier,
    Manager,
    Admin
}

public enum ShiftStatus
{
    Open,
    Closed
}

public enum CashEventType
{
    PaidIn,
    PaidOut,
    NoSale
}

public enum InventoryChangeType
{
    Sale,
    Return,
    Void,
    PhysicalCount,
    Adjustment
}

public enum PointHistoryType
{
    Earn,
    Redeem,
    Revoke,
    Refund,
    Void,
    Adjust
}

public enum TaxRounding
{
    Floor,
    Round,
    Ceiling
}

public enum PointBasis
{
    TaxIncluded,
    TaxExcluded
}
