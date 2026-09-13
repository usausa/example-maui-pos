SELECT
    s.TaxRateId || ':' || s.TaxIncluded AS GroupKey,
    r.Name || (CASE WHEN s.TaxIncluded = 1 THEN ' (内税)' ELSE ' (外税)' END) AS GroupLabel,
    COUNT(DISTINCT CASE WHEN t.Type = 'Sale' THEN t.Id END) AS TransactionCount,
    COUNT(DISTINCT CASE WHEN t.Type = 'Return' THEN t.Id END) AS ReturnCount,
    COUNT(DISTINCT t.CustomerId) AS CustomerCount,
    COALESCE(SUM(CASE WHEN t.Type = 'Sale' THEN s.TaxableAmount ELSE 0 END), 0) AS SalesTotal,
    COALESCE(SUM(CASE WHEN t.Type = 'Return' THEN s.TaxableAmount ELSE 0 END), 0) AS ReturnsTotal,
    COALESCE(SUM(CASE WHEN t.Type = 'Sale' THEN s.TaxableAmount ELSE -s.TaxableAmount END), 0) AS NetSales,
    0 AS DiscountTotal,
    COALESCE(SUM(CASE WHEN t.Type = 'Sale' THEN s.TaxAmount ELSE -s.TaxAmount END), 0) AS TaxTotal,
    0 AS PointsEarned,
    0 AS PointsRedeemed,
    COALESCE(SUM(CASE WHEN t.Type = 'Sale' THEN s.TaxableAmount ELSE -s.TaxableAmount END), 0) AS TaxableAmount,
    COALESCE(SUM(CASE WHEN t.Type = 'Sale' THEN s.TaxAmount ELSE -s.TaxAmount END), 0) AS TaxAmount
FROM TransactionTaxSummaries s
JOIN Transactions t ON t.Id = s.TransactionId
JOIN TaxRates r ON r.Id = s.TaxRateId
WHERE t.Status = 'Completed'
  AND t.BusinessDate >= /*@ from */'' AND t.BusinessDate <= /*@ to */''
/*% if (storeId != null) { */
  AND t.StoreId = /*@ storeId */''
/*% } */
GROUP BY s.TaxRateId, s.TaxIncluded, r.Name, r.SortOrder
ORDER BY r.SortOrder, s.TaxIncluded DESC
