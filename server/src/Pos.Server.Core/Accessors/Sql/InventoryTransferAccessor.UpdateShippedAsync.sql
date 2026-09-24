UPDATE
    InventoryTransfers
SET
    Status = 'Shipped',
    ShippedAt = /*@ shippedAt */'',
    ShippedByStaffId = /*@ staffId */'',
    UpdatedAt = /*@ updatedAt */'',
    Version = Version + 1
WHERE
    Id = /*@ id */''
    AND Status = 'Requested'
RETURNING
    *
