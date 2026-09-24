UPDATE
    Suppliers
SET
    Code = /*@ code */'',
    Name = /*@ name */'',
    Phone = /*@ phone */'',
    Email = /*@ email */'',
    Note = /*@ note */'',
    IsActive = /*@ isActive */1,
    UpdatedAt = /*@ updatedAt */'',
    Version = Version + 1
WHERE
    Id = /*@ id */''
    AND Version = /*@ version */0
    AND IsDeleted = 0
RETURNING
    *
