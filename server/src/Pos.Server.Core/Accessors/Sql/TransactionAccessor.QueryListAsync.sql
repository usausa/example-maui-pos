SELECT
    *
FROM
    Transactions
WHERE
    1 = 1
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
/*% if (serialNumber != null) { */
    AND Id IN (
        SELECT
            l.TransactionId
        FROM
            TransactionLines l
            INNER JOIN TransactionLineSerials s ON s.TransactionLineId = l.Id
        WHERE
            s.SerialNumber = /*@ serialNumber */''
    )
/*% } */
ORDER BY
/*% if (desc) { */
    /*# sort */TransactedAt DESC
/*% } else { */
    /*# sort */TransactedAt
/*% } */
LIMIT /*@ limit */20 OFFSET /*@ offset */0
