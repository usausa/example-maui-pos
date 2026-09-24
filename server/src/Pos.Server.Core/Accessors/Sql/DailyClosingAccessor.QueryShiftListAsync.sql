SELECT
    *
FROM
    Shifts
WHERE
    StoreId = /*@ storeId */''
    AND (
        BusinessDate = /*@ businessDate */''
        OR Id IN (
            SELECT
                ShiftId
            FROM
                Transactions
            WHERE
                StoreId = /*@ storeId */''
                AND BusinessDate = /*@ businessDate */''
        )
    )
ORDER BY
    OpenedAt
