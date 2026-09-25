UPDATE
    Accounts
SET
    Password = /*@ password */NULL,
    UpdatedAt = /*@ updatedAt */'',
    Version = Version + 1
WHERE
    Id = /*@ id */''
    AND Version = /*@ version */0
RETURNING
    *
