INSERT INTO
    ProductImages (ProductId, Data, UpdatedAt)
VALUES
    (/*@ productId */'', /*@ data */'', /*@ updatedAt */'')
ON CONFLICT (ProductId) DO UPDATE SET
    Data = excluded.Data,
    UpdatedAt = excluded.UpdatedAt
