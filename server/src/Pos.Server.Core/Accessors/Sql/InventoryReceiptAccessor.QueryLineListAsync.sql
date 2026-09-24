SELECT
    *
FROM
    InventoryReceiptLines
WHERE
    ReceiptId = /*@ receiptId */''
ORDER BY
    LineNo
