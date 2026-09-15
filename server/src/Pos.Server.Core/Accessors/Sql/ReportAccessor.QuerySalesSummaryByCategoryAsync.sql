SELECT
    l.CategoryId AS GroupKey,
    c.Name AS GroupLabel,
    COUNT(DISTINCT CASE WHEN t.Type = 'Sale' THEN t.Id END) AS TransactionCount,
    COUNT(DISTINCT CASE WHEN t.Type = 'Return' THEN t.Id END) AS ReturnCount,
    COUNT(DISTINCT t.CustomerId) AS CustomerCount,
    COALESCE(SUM(CASE WHEN t.Type = 'Sale' THEN l.NetAmount ELSE 0 END), 0) AS SalesTotal,
    COALESCE(SUM(CASE WHEN t.Type = 'Return' THEN l.NetAmount ELSE 0 END), 0) AS ReturnsTotal,
    COALESCE(SUM(CASE WHEN t.Type = 'Sale' THEN l.NetAmount ELSE -l.NetAmount END), 0) AS NetSales,
    COALESCE(SUM(CASE WHEN t.Type = 'Sale' THEN l.DiscountAmount + l.AllocatedDiscountAmount ELSE -(l.DiscountAmount + l.AllocatedDiscountAmount) END), 0) AS DiscountTotal,
    0 AS TaxTotal,
    COALESCE(SUM(l.PointsEarned), 0) AS PointsEarned,
    COALESCE(SUM(l.PointsRedeemed), 0) AS PointsRedeemed
FROM
    TransactionLines l
    JOIN Transactions t ON t.Id = l.TransactionId
    JOIN Categories c ON c.Id = l.CategoryId
WHERE
    t.Status = 'Completed'
    AND t.BusinessDate >= /*@ from */''
    AND t.BusinessDate <= /*@ to */''
/*% if (storeId != null) { */
    AND t.StoreId = /*@ storeId */''
/*% } */
GROUP BY
    l.CategoryId,
    c.Name,
    c.SortOrder
ORDER BY
    c.SortOrder,
    c.Name
