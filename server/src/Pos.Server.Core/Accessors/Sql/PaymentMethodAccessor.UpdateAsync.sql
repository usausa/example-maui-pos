UPDATE PaymentMethods
SET Code = /*@ code */'',
    Name = /*@ name */'',
    ShortName = /*@ shortName */'',
    Kind = /*@ kind */'',
    AllowsChange = /*@ allowsChange */1,
    RequiresReference = /*@ requiresReference */1,
    IsActive = /*@ isActive */1,
    SortOrder = /*@ sortOrder */0,
    UpdatedAt = /*@ updatedAt */'',
    Version = Version + 1
WHERE Id = /*@ id */'' AND Version = /*@ version */0 AND IsDeleted = 0
