UPDATE
    PurchaseOrders
SET
    Status = 'Cancelled',
    CancelledAt = /*@ cancelledAt */'',
    UpdatedAt = /*@ updatedAt */'',
    Version = Version + 1
WHERE
    ReceiptId = /*@ receiptId */''
    AND Status = 'Ordered'
