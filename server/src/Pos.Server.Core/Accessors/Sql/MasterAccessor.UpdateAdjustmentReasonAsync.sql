UPDATE
    AdjustmentReasons
SET
    Code = /*@ code */'',
    Name = /*@ name */'',
    SortOrder = /*@ sortOrder */0,
    IsActive = /*@ isActive */1,
    UpdatedAt = /*@ updatedAt */'',
    Version = Version + 1
WHERE
    Id = /*@ id */''
    AND Version = /*@ version */0
    AND IsDeleted = 0
RETURNING
    *
