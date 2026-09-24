SELECT
    *
FROM
    InventoryTransferLines
WHERE
    TransferId = /*@ transferId */''
ORDER BY
    LineNo
