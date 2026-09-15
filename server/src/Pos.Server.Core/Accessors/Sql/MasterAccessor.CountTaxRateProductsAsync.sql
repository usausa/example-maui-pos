SELECT
    COUNT(*)
FROM
    Products
WHERE
    TaxRateId = /*@ taxRateId */''
    AND IsDeleted = 0
