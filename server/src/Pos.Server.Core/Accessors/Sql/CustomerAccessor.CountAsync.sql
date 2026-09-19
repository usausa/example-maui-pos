SELECT
    COUNT(*)
FROM
    Customers
WHERE
    1 = 1
/*% if (keyword != null) { */
    AND (
        Code LIKE /*@ keyword */'' ESCAPE '\'
        OR Name LIKE /*@ keyword */'' ESCAPE '\'
        OR Kana LIKE /*@ keyword */'' ESCAPE '\'
        OR Phone LIKE /*@ keyword */'' ESCAPE '\'
    )
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
