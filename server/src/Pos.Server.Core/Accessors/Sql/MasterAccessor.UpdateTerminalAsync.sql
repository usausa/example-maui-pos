UPDATE
    Terminals
SET
    StoreId = /*@ storeId */'',
    TerminalNo = /*@ terminalNo */0,
    Name = /*@ name */'',
    IsActive = /*@ isActive */1,
    UpdatedAt = /*@ updatedAt */'',
    Version = Version + 1
WHERE
    Id = /*@ id */''
    AND Version = /*@ version */0
    AND IsDeleted = 0
RETURNING
    *
