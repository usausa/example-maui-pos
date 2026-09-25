UPDATE
    PurchaseOrders
SET
    Status = 'Ordered',
    OrderedAt = /*@ orderedAt */'',
    OrderedBy = /*@ orderedBy */'',
    ReceiptId = /*@ receiptId */'',
    UpdatedAt = /*@ updatedAt */'',
    Version = Version + 1
WHERE
    Id = /*@ id */''
    AND Status = 'Draft'
RETURNING
    *
