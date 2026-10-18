SELECT
    COUNT(*)
FROM
    Outbox o
    JOIN Transactions t ON t.Id = o.TargetId
WHERE
    o.Status <> 'Sent'
    AND t.OriginalTransactionId = /*@ originalTransactionId */''
