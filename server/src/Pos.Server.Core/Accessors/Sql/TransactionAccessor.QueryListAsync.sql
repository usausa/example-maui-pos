SELECT * FROM Transactions
WHERE 1 = 1
/*% if (storeId != null) { */
  AND StoreId = /*@ storeId */''
/*% } */
/*% if (terminalId != null) { */
  AND TerminalId = /*@ terminalId */''
/*% } */
/*% if (staffId != null) { */
  AND StaffId = /*@ staffId */''
/*% } */
/*% if (shiftId != null) { */
  AND ShiftId = /*@ shiftId */''
/*% } */
/*% if (customerId != null) { */
  AND CustomerId = /*@ customerId */''
/*% } */
/*% if (from != null) { */
  AND BusinessDate >= /*@ from */''
/*% } */
/*% if (to != null) { */
  AND BusinessDate <= /*@ to */''
/*% } */
/*% if (type != null) { */
  AND Type = /*@ type */''
/*% } */
/*% if (status != null) { */
  AND Status = /*@ status */''
/*% } */
ORDER BY /*# sort */Id
LIMIT /*@ limit */20 OFFSET /*@ offset */0
