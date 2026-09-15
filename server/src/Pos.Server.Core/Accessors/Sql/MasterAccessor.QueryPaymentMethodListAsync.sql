SELECT
    *
FROM
    PaymentMethods
WHERE
    1 = 1
/*% if (updatedSince != null) { */
    AND UpdatedAt > /*@ updatedSince */''
/*% } */
/*% if (!includeDeleted) { */
    AND IsDeleted = 0
/*% } */
ORDER BY
    SortOrder,
    Code
