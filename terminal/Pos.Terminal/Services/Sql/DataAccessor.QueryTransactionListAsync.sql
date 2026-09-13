SELECT * FROM Transactions
WHERE 1 = 1
/*% if (shiftId != null) { */
  AND ShiftId = /*@ shiftId */''
/*% } */
/*% if (businessDate != null) { */
  AND BusinessDate = /*@ businessDate */''
/*% } */
/*% if (type != null) { */
  AND Type = /*@ type */''
/*% } */
ORDER BY TransactedAt DESC
LIMIT /*@ limit */100
