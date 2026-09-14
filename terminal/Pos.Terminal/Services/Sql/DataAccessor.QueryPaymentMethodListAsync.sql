SELECT
    *
FROM
    PaymentMethods
WHERE
    IsActive = 1
    AND IsDeleted = 0
ORDER BY
    SortOrder,
    Code
