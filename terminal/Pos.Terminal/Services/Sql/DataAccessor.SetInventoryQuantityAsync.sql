INSERT INTO InventoryLevels (StoreId, ProductId, Quantity, UpdatedAt)
VALUES (/*@ storeId */'', /*@ productId */'', /*@ quantity */0, /*@ updatedAt */0)
ON CONFLICT (StoreId, ProductId) DO UPDATE SET Quantity = excluded.Quantity, UpdatedAt = excluded.UpdatedAt
