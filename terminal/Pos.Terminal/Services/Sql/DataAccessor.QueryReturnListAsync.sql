SELECT
    *
FROM
    Transactions
WHERE
    OriginalTransactionId = /*@ originalTransactionId */''
    AND Status = 'Completed'
