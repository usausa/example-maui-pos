UPDATE
    InventoryTransferLines
SET
    ReceivedQuantity = /*@ receivedQuantity */0
WHERE
    Id = /*@ id */''
