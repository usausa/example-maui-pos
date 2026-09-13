UPDATE TaxRates
SET IsDefault = 0,
    UpdatedAt = /*@ updatedAt */'',
    Version = Version + 1
WHERE IsDefault = 1 AND Id <> /*@ exceptId */'' AND IsDeleted = 0
