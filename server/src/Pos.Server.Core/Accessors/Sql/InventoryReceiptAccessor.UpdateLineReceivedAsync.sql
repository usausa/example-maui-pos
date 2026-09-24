UPDATE
    InventoryReceiptLines
SET
    ReceivedQuantity = /*@ receivedQuantity */0
WHERE
    Id = /*@ id */''
