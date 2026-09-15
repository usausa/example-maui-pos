SELECT
    *
FROM
    Terminals
WHERE
    1 = 1
/*% if (storeId != null) { */
    AND StoreId = /*@ storeId */''
/*% } */
/*% if (updatedSince != null) { */
    AND UpdatedAt > /*@ updatedSince */''
/*% } */
/*% if (!includeDeleted) { */
    AND IsDeleted = 0
/*% } */
ORDER BY
/*% if (updatedSince != null) { */
    UpdatedAt,
    Id
/*% } else if (desc) { */
    /*# sort.ToString() */TerminalNo DESC
/*% } else { */
    /*# sort.ToString() */TerminalNo
/*% } */
LIMIT /*@ limit */20 OFFSET /*@ offset */0
