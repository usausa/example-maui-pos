UPDATE
    Products
SET
    ImageUrl = /*@ imageLocation */'',
    UpdatedAt = /*@ updatedAt */'',
    Version = Version + 1
WHERE
    Id = /*@ id */''
    AND IsDeleted = 0
RETURNING
    *
