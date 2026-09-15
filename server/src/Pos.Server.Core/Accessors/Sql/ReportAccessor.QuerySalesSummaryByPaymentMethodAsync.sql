SELECT
    p.PaymentMethodId AS GroupKey,
    m.Name AS GroupLabel,
    COUNT(DISTINCT CASE WHEN t.Type = 'Sale' THEN t.Id END) AS TransactionCount,
    COUNT(DISTINCT CASE WHEN t.Type = 'Return' THEN t.Id END) AS ReturnCount,
    COUNT(DISTINCT t.CustomerId) AS CustomerCount,
    COALESCE(SUM(CASE WHEN t.Type = 'Sale' THEN p.Amount ELSE 0 END), 0) AS SalesTotal,
    COALESCE(SUM(CASE WHEN t.Type = 'Return' THEN p.Amount ELSE 0 END), 0) AS ReturnsTotal,
    COALESCE(SUM(CASE WHEN t.Type = 'Sale' THEN p.Amount ELSE -p.Amount END), 0) AS NetSales,
    0 AS DiscountTotal,
    0 AS TaxTotal,
    0 AS PointsEarned,
    0 AS PointsRedeemed
FROM
    TransactionPayments p
    JOIN Transactions t ON t.Id = p.TransactionId
    JOIN PaymentMethods m ON m.Id = p.PaymentMethodId
WHERE
    t.Status = 'Completed'
    AND t.BusinessDate >= /*@ from */''
    AND t.BusinessDate <= /*@ to */''
/*% if (storeId != null) { */
    AND t.StoreId = /*@ storeId */''
/*% } */
GROUP BY
    p.PaymentMethodId,
    m.Name,
    m.SortOrder
ORDER BY
    m.SortOrder,
    m.Name
