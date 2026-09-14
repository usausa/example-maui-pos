SELECT
    /*# Pos.Server.Accessors.SqlHelper.GroupKey(groupBy) */t.BusinessDate AS GroupKey,
    /*# Pos.Server.Accessors.SqlHelper.GroupLabel(groupBy) */t.BusinessDate AS GroupLabel,
    SUM(CASE WHEN t.Type = 'Sale' THEN 1 ELSE 0 END) AS TransactionCount,
    SUM(CASE WHEN t.Type = 'Return' THEN 1 ELSE 0 END) AS ReturnCount,
    COUNT(DISTINCT t.CustomerId) AS CustomerCount,
    COALESCE(SUM(CASE WHEN t.Type = 'Sale' THEN t.Total ELSE 0 END), 0) AS SalesTotal,
    COALESCE(SUM(CASE WHEN t.Type = 'Return' THEN t.Total ELSE 0 END), 0) AS ReturnsTotal,
    COALESCE(SUM(CASE WHEN t.Type = 'Sale' THEN t.Total ELSE -t.Total END), 0) AS NetSales,
    COALESCE(SUM(CASE WHEN t.Type = 'Sale' THEN t.DiscountTotal ELSE -t.DiscountTotal END), 0) AS DiscountTotal,
    COALESCE(SUM(CASE WHEN t.Type = 'Sale' THEN t.TaxTotal ELSE -t.TaxTotal END), 0) AS TaxTotal,
    COALESCE(SUM(t.PointsEarned), 0) AS PointsEarned,
    COALESCE(SUM(t.PointsRedeemed), 0) AS PointsRedeemed
FROM Transactions t
LEFT JOIN Stores st ON st.Id = t.StoreId
LEFT JOIN Terminals tm ON tm.Id = t.TerminalId
LEFT JOIN Staff s ON s.Id = t.StaffId
WHERE t.Status = 'Completed'
  AND t.BusinessDate >= /*@ from */'' AND t.BusinessDate <= /*@ to */''
/*% if (storeId != null) { */
  AND t.StoreId = /*@ storeId */''
/*% } */
GROUP BY GroupKey, GroupLabel
ORDER BY GroupKey
