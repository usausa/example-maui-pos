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
    AND COALESCE((
        SELECT
            SUM(CASE WHEN d.Type = 'Receive' THEN d.Amount ELSE -d.Amount END)
        FROM
            OrderDeposits d
        WHERE
            d.OrderId = Orders.Id
    ), 0) = CAST(/*@ depositApplied */0 AS NUMERIC)
