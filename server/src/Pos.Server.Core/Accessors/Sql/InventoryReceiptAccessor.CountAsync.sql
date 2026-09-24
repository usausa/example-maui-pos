SELECT
    COUNT(*)
FROM
    InventoryReceipts
WHERE
    1 = 1
/*% if (storeId != null) { */
    AND StoreId = /*@ storeId */''
/*% } */
/*% if (supplierId != null) { */
    AND SupplierId = /*@ supplierId */''
/*% } */
/*% if (status != null) { */
    AND Status = /*@ status */''
/*% } */
/*% if (from != null) { */
    AND ExpectedDate >= /*@ from */''
/*% } */
/*% if (to != null) { */
    AND ExpectedDate <= /*@ to */''
/*% } */
