UPDATE
    InventoryTransfers
SET
    Status = 'Cancelled',
    CancelledAt = /*@ cancelledAt */'',
    UpdatedAt = /*@ updatedAt */'',
    Version = Version + 1
WHERE
    Id = /*@ id */''
    AND Status = 'Requested'
RETURNING
    *
