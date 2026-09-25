UPDATE
    PurchaseOrders
SET
    Status = 'Received',
    UpdatedAt = /*@ updatedAt */'',
    Version = Version + 1
WHERE
    ReceiptId = /*@ receiptId */''
    AND Status = 'Ordered'
