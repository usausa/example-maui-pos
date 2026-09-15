UPDATE
    TransactionLines
SET
    ReturnedQuantity = ReturnedQuantity + /*@ quantity */0
WHERE
    Id = /*@ lineId */''
    AND ReturnedQuantity + /*@ quantity */0 <= Quantity
