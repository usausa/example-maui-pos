SELECT
    *
FROM
    PurchaseOrderLines
WHERE
    PurchaseOrderId = /*@ purchaseOrderId */''
ORDER BY
    LineNo
