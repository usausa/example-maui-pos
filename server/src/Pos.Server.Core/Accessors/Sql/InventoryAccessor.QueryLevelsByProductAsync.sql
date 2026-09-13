SELECT i.StoreId, s.Name AS StoreName, i.Quantity, i.UpdatedAt
FROM InventoryLevels i
JOIN Stores s ON s.Id = i.StoreId
WHERE i.ProductId = /*@ productId */''
ORDER BY s.Code
