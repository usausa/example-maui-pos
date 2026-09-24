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
RETURNING
    *
