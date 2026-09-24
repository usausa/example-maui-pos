UPDATE
    Orders
SET
    Status = 'Arrived',
    TransactionId = NULL,
    CompletedAt = NULL,
    UpdatedAt = /*@ updatedAt */'',
    Version = Version + 1
WHERE
    TransactionId = /*@ transactionId */''
    AND Status = 'Completed'
