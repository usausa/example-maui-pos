UPDATE Staff
SET Code = /*@ code */'',
    Name = /*@ name */'',
    Role = /*@ role */'',
    StoreId = /*@ storeId */'',
    IsActive = /*@ isActive */1,
    UpdatedAt = /*@ updatedAt */'',
    Version = Version + 1
WHERE Id = /*@ id */'' AND Version = /*@ version */0 AND IsDeleted = 0
