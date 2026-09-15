SELECT
    *
FROM
    Staff
WHERE
    1 = 1
/*% if (!includeDeleted) { */
    AND IsDeleted = 0
/*% } */
ORDER BY
    Code
