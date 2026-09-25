UPDATE
    PurchaseOrders
SET
    Status = 'Cancelled',
    CancelledAt = /*@ cancelledAt */'',
    UpdatedAt = /*@ updatedAt */'',
    Version = Version + 1
WHERE
    Id = /*@ id */''
    AND Status IN ('Draft', 'Ordered')
RETURNING
    *
