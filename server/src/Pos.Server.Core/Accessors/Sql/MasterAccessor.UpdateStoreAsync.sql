UPDATE
    Stores
SET
    Code = /*@ code */'',
    Name = /*@ name */'',
    PostalCode = /*@ postalCode */'',
    Address = /*@ address */'',
    Phone = /*@ phone */'',
    RegistrationNo = /*@ registrationNo */'',
    ReceiptHeader = /*@ receiptHeader */'',
    ReceiptFooter = /*@ receiptFooter */'',
    TimeZone = /*@ timeZone */'',
    IsActive = /*@ isActive */1,
    UpdatedAt = /*@ updatedAt */'',
    Version = Version + 1
WHERE
    Id = /*@ id */''
    AND Version = /*@ version */0
    AND IsDeleted = 0
RETURNING
    *
