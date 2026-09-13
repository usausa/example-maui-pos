INSERT INTO InventoryLevels (StoreId, ProductId, Quantity, UpdatedAt)
VALUES (/*@ storeId */'', /*@ productId */'', /*@ delta */0, /*@ updatedAt */0)
ON CONFLICT (StoreId, ProductId) DO UPDATE SET Quantity = Quantity + excluded.Quantity, UpdatedAt = excluded.UpdatedAt
