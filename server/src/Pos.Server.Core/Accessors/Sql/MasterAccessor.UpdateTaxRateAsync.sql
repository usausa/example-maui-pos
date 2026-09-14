UPDATE TaxRates
SET Code = /*@ code */'',
    Name = /*@ name */'',
    Rate = /*@ rate */0,
    Kind = /*@ kind */'',
    IsDefault = /*@ isDefault */1,
    SortOrder = /*@ sortOrder */0,
    UpdatedAt = /*@ updatedAt */'',
    Version = Version + 1
WHERE Id = /*@ id */'' AND Version = /*@ version */0 AND IsDeleted = 0
