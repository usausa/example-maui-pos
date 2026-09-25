UPDATE
    Accounts
SET
    Role = /*@ role */'',
    IsActive = /*@ isActive */1,
    UpdatedAt = /*@ updatedAt */'',
    Version = Version + 1
WHERE
    Id = /*@ id */''
    AND Version = /*@ version */0
RETURNING
    *
