UPDATE
    Shifts
SET
    Status = 'Closed',
    ClosedAt = /*@ closedAt */'',
    ClosedByStaffId = /*@ closedByStaffId */'',
    ActualCash = /*@ actualCash */0,
    ExpectedCash = /*@ expectedCash */0,
    Difference = /*@ difference */0,
    CashSales = /*@ totals.CashSales */0,
    CashReturns = /*@ totals.CashReturns */0,
    PaidIn = /*@ totals.PaidIn */0,
    PaidOut = /*@ totals.PaidOut */0,
    SalesCount = /*@ totals.SalesCount */0,
    ReturnCount = /*@ totals.ReturnCount */0,
    VoidCount = /*@ totals.VoidCount */0,
    SalesTotal = /*@ totals.SalesTotal */0,
    ReturnsTotal = /*@ totals.ReturnsTotal */0,
    Note = /*@ note */'',
    UpdatedAt = /*@ updatedAt */''
WHERE
    Id = /*@ id */''
    AND Status = 'Open'
