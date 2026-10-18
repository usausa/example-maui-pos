UPDATE
    Transactions
SET
    Status = 'Voided',
    VoidedAt = /*@ voidedAt */'',
    VoidedByStaffId = /*@ voidedByStaffId */'',
    VoidReason = /*@ reason */'',
    UpdatedAt = /*@ updatedAt */''
WHERE
    Id = /*@ id */''
    AND Status = 'Completed'
    AND EXISTS (
        SELECT
            1
        FROM
            Shifts s
        WHERE
            s.Id = Transactions.ShiftId
            AND s.Status = 'Open'
    )
    AND NOT EXISTS (
        SELECT
            1
        FROM
            DailyClosings d
        WHERE
            d.StoreId = Transactions.StoreId
            AND d.BusinessDate = Transactions.BusinessDate
    )
    AND (
        Type <> 'Sale'
        OR NOT EXISTS (
            SELECT
                1
            FROM
                Transactions r
            WHERE
                r.OriginalTransactionId = /*@ id */''
                AND r.Status = 'Completed'
        )
    )
RETURNING
    *
