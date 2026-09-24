UPDATE
    Orders
SET
    Status = 'Completed',
    TransactionId = /*@ transactionId */'',
    CompletedAt = /*@ completedAt */'',
    UpdatedAt = /*@ updatedAt */'',
    Version = Version + 1
WHERE
    Id = /*@ id */''
    AND Status = 'Arrived'
