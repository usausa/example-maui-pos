SELECT
    i.*
FROM
    InventoryLevels i
    JOIN Products p ON p.Id = i.ProductId
WHERE
    1 = 1
/*% if (storeId != null) { */
    AND i.StoreId = /*@ storeId */''
/*% } */
/*% if (productId != null) { */
    AND i.ProductId = /*@ productId */''
/*% } */
/*% if (categoryId != null) { */
    AND p.CategoryId = /*@ categoryId */''
/*% } */
/*% if (negativeOnly) { */
    AND i.Quantity < 0
/*% } */
/*% if (updatedSince != null) { */
    AND i.UpdatedAt > /*@ updatedSince */''
/*% } */
/*% if (updatedSince != null) { */
ORDER BY
    i.UpdatedAt,
    i.StoreId,
    i.ProductId
/*% } else { */
ORDER BY
    p.Code,
    i.StoreId
/*% } */
LIMIT /*@ limit */20 OFFSET /*@ offset */0
