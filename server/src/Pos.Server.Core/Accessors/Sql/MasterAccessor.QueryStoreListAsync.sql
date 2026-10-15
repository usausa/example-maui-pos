SELECT
    *
FROM
    Stores
WHERE
    1 = 1
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
    /*# sort */Code DESC,
    Code DESC
/*% } else { */
    /*# sort */Code,
    Code
/*% } */
LIMIT /*@ limit */20 OFFSET /*@ offset */0
