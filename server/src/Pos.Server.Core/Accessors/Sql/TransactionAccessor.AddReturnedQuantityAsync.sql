UPDATE
    TransactionLines
SET
    ReturnedQuantity = ReturnedQuantity + /*@ quantity */0
WHERE
    Id = /*@ lineId */''
    AND ReturnedQuantity + /*@ quantity */0 <= Quantity
    AND EXISTS (
        SELECT
            1
        FROM
            Transactions t
        WHERE
            t.Id = TransactionLines.TransactionId
            AND t.Status = 'Completed'
    )
