UPDATE
    Customers
SET
    Code = /*@ code */'',
    Name = /*@ name */'',
    Kana = /*@ kana */'',
    Phone = /*@ phone */'',
    Email = /*@ email */'',
    PostalCode = /*@ postalCode */'',
    Address = /*@ address */'',
    BirthDate = /*@ birthDate */'',
    Note = /*@ note */'',
    UpdatedAt = /*@ updatedAt */'',
    Version = Version + 1
WHERE
    Id = /*@ id */''
    AND Version = /*@ version */0
    AND IsDeleted = 0
RETURNING
    *
