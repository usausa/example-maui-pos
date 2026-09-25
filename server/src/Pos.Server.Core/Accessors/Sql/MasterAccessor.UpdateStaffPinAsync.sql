UPDATE
    Staff
SET
    PinHash = /*@ pinHash */NULL,
    UpdatedAt = /*@ updatedAt */'',
    Version = Version + 1
WHERE
    Id = /*@ id */''
    AND Version = /*@ version */0
    AND IsDeleted = 0
RETURNING
    *
