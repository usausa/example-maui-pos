SELECT
    *
FROM
    Staff
WHERE
    1 = 1
/*% if (storeId != null) { */
    AND (
        StoreId = /*@ storeId */''
        OR StoreId IS NULL
    )
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
    /*# sort */Code DESC
/*% } else { */
    /*# sort */Code
/*% } */
LIMIT /*@ limit */20 OFFSET /*@ offset */0
