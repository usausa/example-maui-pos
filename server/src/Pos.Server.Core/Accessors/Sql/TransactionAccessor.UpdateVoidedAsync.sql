UPDATE
    Transactions
SET
    Status = 'Voided',
    VoidedAt = /*@ voidedAt */'',
    VoidedByStaffId = /*@ voidedByStaffId */'',
    VoidReason = /*@ reason */'',
    UpdatedAt = /*@ updatedAt */''
WHERE
    Id = /*@ id */''
    AND Status = 'Completed'
