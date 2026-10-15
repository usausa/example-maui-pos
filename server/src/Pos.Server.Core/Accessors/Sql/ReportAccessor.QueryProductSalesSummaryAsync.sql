/*!helper Pos.Server.Accessors.SqlHelper */
SELECT
    l.ProductId,
    p.Code AS ProductCode,
    p.Name AS ProductName,
    p.CategoryId,
    c.Name AS CategoryName,
    COALESCE(SUM(CASE WHEN t.Type = 'Sale' THEN l.Quantity ELSE 0 END), 0) AS QuantitySold,
    COALESCE(SUM(CASE WHEN t.Type = 'Return' THEN l.Quantity ELSE 0 END), 0) AS QuantityReturned,
    COALESCE(SUM(CASE WHEN t.Type = 'Sale' THEN l.Quantity ELSE -l.Quantity END), 0) AS NetQuantity,
    COALESCE(SUM(CASE WHEN t.Type = 'Sale' THEN l.NetAmount ELSE 0 END), 0) AS SalesTotal,
    COALESCE(SUM(CASE WHEN t.Type = 'Return' THEN l.NetAmount ELSE 0 END), 0) AS ReturnsTotal,
    COALESCE(SUM(CASE WHEN t.Type = 'Sale' THEN l.NetAmount ELSE -l.NetAmount END), 0) AS NetSales,
    COALESCE(SUM(CASE WHEN t.Type = 'Sale' THEN l.DiscountAmount + l.AllocatedDiscountAmount ELSE -(l.DiscountAmount + l.AllocatedDiscountAmount) END), 0) AS DiscountTotal,
    CASE
        WHEN p.Cost IS NULL THEN NULL
        ELSE COALESCE(SUM(CASE WHEN t.Type = 'Sale' THEN l.NetAmount ELSE -l.NetAmount END), 0) - p.Cost * COALESCE(SUM(CASE WHEN t.Type = 'Sale' THEN l.Quantity ELSE -l.Quantity END), 0)
    END AS GrossProfit
FROM
    TransactionLines l
    JOIN Transactions t ON t.Id = l.TransactionId
    JOIN Products p ON p.Id = l.ProductId
    JOIN Categories c ON c.Id = p.CategoryId
WHERE
    t.Status = 'Completed'
    AND t.BusinessDate >= /*@ from */''
    AND t.BusinessDate <= /*@ to */''
/*% if (storeId != null) { */
    AND t.StoreId = /*@ storeId */''
/*% } */
/*% if (categoryId != null) { */
    AND p.CategoryId = /*@ categoryId */''
/*% } */
GROUP BY
    l.ProductId,
    p.Code,
    p.Name,
    p.CategoryId,
    c.Name,
    p.Cost
ORDER BY
    /*# ProductSalesColumn(sort) */NetSales DESC,
    ProductCode
LIMIT /*@ limit */50
