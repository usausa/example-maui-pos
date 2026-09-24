UPDATE
    InventoryTransfers
SET
    Status = 'Received',
    ReceivedAt = /*@ receivedAt */'',
    ReceivedByStaffId = /*@ staffId */'',
    UpdatedAt = /*@ updatedAt */'',
    Version = Version + 1
WHERE
    Id = /*@ id */''
    AND Status = 'Shipped'
RETURNING
    *
