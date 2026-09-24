SELECT
    COUNT(*)
FROM
    InventoryTransfers
WHERE
    1 = 1
/*% if (storeId != null) { */
    AND (FromStoreId = /*@ storeId */'' OR ToStoreId = /*@ storeId */'')
/*% } */
/*% if (fromStoreId != null) { */
    AND FromStoreId = /*@ fromStoreId */''
/*% } */
/*% if (toStoreId != null) { */
    AND ToStoreId = /*@ toStoreId */''
/*% } */
/*% if (status != null) { */
    AND Status = /*@ status */''
/*% } */
/*% if (openOnly) { */
    AND Status IN ('Requested', 'Shipped')
/*% } */
