UPDATE Settings
SET CompanyName = /*@ companyName */'',
    Currency = /*@ currency */'',
    TaxRounding = /*@ taxRounding */'',
    PointBasis = /*@ pointBasis */'',
    BusinessDayStartTime = /*@ businessDayStartTime */'',
    UpdatedAt = /*@ updatedAt */'',
    Version = Version + 1
WHERE Id = 1 AND Version = /*@ version */0
