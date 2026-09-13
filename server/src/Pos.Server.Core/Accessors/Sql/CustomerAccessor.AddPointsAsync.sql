UPDATE Customers
SET PointBalance = PointBalance + /*@ delta */0,
    UpdatedAt = /*@ updatedAt */''
WHERE Id = /*@ id */''
RETURNING PointBalance
