UPDATE
    Categories
SET
    Code = /*@ code */'',
    Name = /*@ name */'',
    ParentId = /*@ parentId */'',
    SortOrder = /*@ sortOrder */0,
    UpdatedAt = /*@ updatedAt */'',
    Version = Version + 1
WHERE
    Id = /*@ id */''
    AND Version = /*@ version */0
    AND IsDeleted = 0
RETURNING
    *
