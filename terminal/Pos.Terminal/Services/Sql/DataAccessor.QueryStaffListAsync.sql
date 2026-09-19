SELECT
    *
FROM
    Staff
WHERE
    IsActive = 1
    AND IsDeleted = 0
    AND (
        StoreId IS NULL
        OR StoreId = /*@ storeId */''
    )
ORDER BY
    Code
