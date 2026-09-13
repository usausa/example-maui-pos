UPDATE Discounts
SET IsDeleted = 1,
    UpdatedAt = /*@ updatedAt */'',
    Version = Version + 1
WHERE Id = /*@ id */'' AND IsDeleted = 0
