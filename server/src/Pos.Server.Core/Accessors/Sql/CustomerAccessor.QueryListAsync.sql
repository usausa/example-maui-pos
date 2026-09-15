SELECT
    *
FROM
    Customers
WHERE
    1 = 1
/*% if (keyword != null) { */
    AND (Code LIKE /*@ keyword */'' ESCAPE '\' OR Name LIKE /*@ keyword */'' ESCAPE '\' OR Kana LIKE /*@ keyword */'' ESCAPE '\' OR Phone LIKE /*@ keyword */'' ESCAPE '\')
/*% } */
/*% if (code != null) { */
    AND Code = /*@ code */''
/*% } */
/*% if (phone != null) { */
    AND Phone = /*@ phone */''
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
