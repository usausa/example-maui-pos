UPDATE
    Orders
SET
    Status = 'Cancelled',
    CancelledAt = /*@ cancelledAt */'',
    CancelReason = /*@ cancelReason */'',
    UpdatedAt = /*@ updatedAt */'',
    Version = Version + 1
WHERE
    Id = /*@ id */''
    AND Status IN ('Ordered', 'Arrived')
    AND COALESCE((
        SELECT
            SUM(CASE WHEN d.Type = 'Receive' THEN d.Amount ELSE -d.Amount END)
        FROM
            OrderDeposits d
        WHERE
            d.OrderId = Orders.Id
    ), 0) = 0
RETURNING
    *
