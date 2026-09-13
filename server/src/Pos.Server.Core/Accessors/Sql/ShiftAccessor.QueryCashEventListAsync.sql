SELECT * FROM CashEvents
WHERE ShiftId = /*@ shiftId */''
ORDER BY OccurredAt, Id
LIMIT /*@ limit */20 OFFSET /*@ offset */0
