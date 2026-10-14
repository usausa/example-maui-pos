UPDATE
    Shifts
SET
    Status = 'Closed',
    ClosedAt = /*@ closedAt */'',
    ClosedByStaffId = /*@ closedByStaffId */'',
    ActualCash = /*@ actualCash */0,
    ExpectedCash = /*@ expectedCash */0,
    Difference = /*@ difference */0,
    CashSales = /*@ cashSales */0,
    CashReturns = /*@ cashReturns */0,
    PaidIn = /*@ paidIn */0,
    PaidOut = /*@ paidOut */0,
    DepositCashIn = /*@ depositCashIn */0,
    DepositCashOut = /*@ depositCashOut */0,
    SalesCount = /*@ salesCount */0,
    ReturnCount = /*@ returnCount */0,
    VoidCount = /*@ voidCount */0,
    SalesTotal = /*@ salesTotal */0,
    ReturnsTotal = /*@ returnsTotal */0,
    Note = /*@ note */'',
    UpdatedAt = /*@ updatedAt */''
WHERE
    Id = /*@ id */''
    AND Status = 'Open'
RETURNING
    *
