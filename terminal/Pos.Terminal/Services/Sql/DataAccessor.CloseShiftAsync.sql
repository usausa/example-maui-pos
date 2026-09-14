UPDATE Shifts
SET
    Status = 'Closed',
    ClosedAt = /*@ closedAt */0,
    ClosedByStaffId = /*@ closedByStaffId */'',
    ActualCash = /*@ actualCash */0,
    ExpectedCash = /*@ expectedCash */0,
    Difference = /*@ difference */0,
    Note = /*@ note */''
WHERE
    Id = /*@ id */''
