SELECT
    *
FROM
    InventoryChanges
WHERE
    1 = 1
/*% if (storeId != null) { */
    AND StoreId = /*@ storeId */''
/*% } */
/*% if (productId != null) { */
    AND ProductId = /*@ productId */''
/*% } */
/*% if (type != null) { */
    AND Type = /*@ type */''
/*% } */
/*% if (from != null) { */
    AND OccurredAt >= /*@ from */''
/*% } */
/*% if (to != null) { */
    AND OccurredAt < /*@ to */''
/*% } */
ORDER BY
    OccurredAt DESC,
    Id DESC
LIMIT /*@ limit */20 OFFSET /*@ offset */0
