SELECT
    i.StoreId,
    s.Name AS StoreName,
    i.ProductId,
    p.Code AS ProductCode,
    p.Name AS ProductName,
    p.CategoryId,
    c.Name AS CategoryName,
    i.Quantity AS Quantity,
    i.UpdatedAt AS UpdatedAt
FROM
    InventoryLevels i
    JOIN Products p ON p.Id = i.ProductId
    JOIN Stores s ON s.Id = i.StoreId
    JOIN Categories c ON c.Id = p.CategoryId
WHERE
    1 = 1
/*% if (storeId != null) { */
    AND i.StoreId = /*@ storeId */''
/*% } */
/*% if (categoryId != null) { */
    AND p.CategoryId = /*@ categoryId */''
/*% } */
/*% if (keyword != null) { */
    AND (p.Code LIKE /*@ keyword */'' ESCAPE '\' OR p.Name LIKE /*@ keyword */'' ESCAPE '\' OR p.Kana LIKE /*@ keyword */'' ESCAPE '\' OR p.Barcode LIKE /*@ keyword */'' ESCAPE '\')
/*% } */
/*% if (negativeOnly) { */
    AND i.Quantity < 0
/*% } */
ORDER BY
/*% if (desc) { */
    /*# sort.ToString() */ProductCode DESC
/*% } else { */
    /*# sort.ToString() */ProductCode
/*% } */
LIMIT /*@ limit */20 OFFSET /*@ offset */0
