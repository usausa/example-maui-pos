SELECT
    *
FROM
    Shifts
WHERE
    1 = 1
/*% if (storeId != null) { */
    AND StoreId = /*@ storeId */''
/*% } */
/*% if (terminalId != null) { */
    AND TerminalId = /*@ terminalId */''
/*% } */
/*% if (status != null) { */
    AND Status = /*@ status */''
/*% } */
/*% if (from != null) { */
    AND BusinessDate >= /*@ from */''
/*% } */
/*% if (to != null) { */
    AND BusinessDate <= /*@ to */''
/*% } */
ORDER BY
/*% if (desc) { */
    /*# sort.ToString() */OpenedAt DESC
/*% } else { */
    /*# sort.ToString() */OpenedAt
/*% } */
LIMIT /*@ limit */20 OFFSET /*@ offset */0
