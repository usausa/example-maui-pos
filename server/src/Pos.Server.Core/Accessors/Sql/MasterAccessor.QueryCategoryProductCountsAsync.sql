SELECT
    CategoryId,
    COUNT(*) AS Count
FROM
    Products
WHERE
    IsDeleted = 0
GROUP BY
    CategoryId
