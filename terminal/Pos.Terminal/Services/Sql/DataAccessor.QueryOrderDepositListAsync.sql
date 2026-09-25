SELECT
    *
FROM
    OrderDeposits
WHERE
    ShiftId = /*@ shiftId */''
ORDER BY
    OccurredAt
