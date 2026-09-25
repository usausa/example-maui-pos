UPDATE
    PurchaseOrders
SET
    SupplierId = /*@ supplierId */'',
    ExpectedDate = /*@ expectedDate */'',
    Note = /*@ note */'',
    UpdatedAt = /*@ updatedAt */'',
    Version = Version + 1
WHERE
    Id = /*@ id */''
    AND Version = /*@ version */0
    AND Status = 'Draft'
RETURNING
    *
