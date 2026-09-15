SELECT
    *
FROM
    Categories
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
    /*# sort.ToString() */SortOrder DESC
/*% } else { */
    /*# sort.ToString() */SortOrder
/*% } */
LIMIT /*@ limit */20 OFFSET /*@ offset */0
