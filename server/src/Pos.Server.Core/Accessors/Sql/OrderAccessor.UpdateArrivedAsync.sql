UPDATE
    Orders
SET
    Status = 'Arrived',
    ArrivedAt = /*@ arrivedAt */'',
    UpdatedAt = /*@ updatedAt */'',
    Version = Version + 1
WHERE
    Id = /*@ id */''
    AND Status = 'Ordered'
RETURNING
    *
