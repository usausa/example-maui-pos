SELECT
    *
FROM
    Products
WHERE
    IsActive = 1
    AND IsDeleted = 0
/*% if (categoryIds != null) { */
    AND CategoryId IN /*@ categoryIds */('')
/*% } */
/*% if (keyword != null) { */
    AND (
        Code LIKE /*@ keyword */'' ESCAPE '\'
        OR Barcode LIKE /*@ keyword */'' ESCAPE '\'
        OR Name LIKE /*@ keyword */'' ESCAPE '\'
        OR Kana LIKE /*@ keyword */'' ESCAPE '\'
        OR ModelNo LIKE /*@ keyword */'' ESCAPE '\'
    )
/*% } */
ORDER BY
    Code
LIMIT /*@ limit */50
