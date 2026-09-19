SELECT
    COUNT(*)
FROM
    InventoryLevels i
    JOIN Products p ON p.Id = i.ProductId
WHERE
    1 = 1
/*% if (storeId != null) { */
    AND i.StoreId = /*@ storeId */''
/*% } */
/*% if (categoryId != null) { */
    AND p.CategoryId = /*@ categoryId */''
/*% } */
/*% if (keyword != null) { */
    AND (
        p.Code LIKE /*@ keyword */'' ESCAPE '\'
        OR p.Name LIKE /*@ keyword */'' ESCAPE '\'
        OR p.Kana LIKE /*@ keyword */'' ESCAPE '\'
        OR p.Barcode LIKE /*@ keyword */'' ESCAPE '\'
    )
/*% } */
/*% if (negativeOnly) { */
    AND i.Quantity < 0
/*% } */
