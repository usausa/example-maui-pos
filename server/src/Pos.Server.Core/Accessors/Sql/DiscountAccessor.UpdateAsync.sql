UPDATE Discounts
SET Code = /*@ code */'',
    Name = /*@ name */'',
    Type = /*@ type */'',
    Value = /*@ value */0,
    Scope = /*@ scope */'',
    RequiresApproval = /*@ requiresApproval */1,
    IsActive = /*@ isActive */1,
    SortOrder = /*@ sortOrder */0,
    UpdatedAt = /*@ updatedAt */'',
    Version = Version + 1
WHERE Id = /*@ id */'' AND Version = /*@ version */0 AND IsDeleted = 0
