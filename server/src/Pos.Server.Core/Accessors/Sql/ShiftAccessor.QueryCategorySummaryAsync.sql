SELECT
    l.CategoryId,
    c.Name,
    COALESCE(SUM(CASE WHEN t.Type = 'Sale' THEN l.Quantity ELSE -l.Quantity END), 0) AS Quantity,
    COALESCE(SUM(CASE WHEN t.Type = 'Sale' THEN l.NetAmount ELSE -l.NetAmount END), 0) AS NetAmount
FROM
    TransactionLines l
    JOIN Transactions t ON t.Id = l.TransactionId
    JOIN Categories c ON c.Id = l.CategoryId
WHERE
    t.ShiftId = /*@ shiftId */''
    AND t.Status = 'Completed'
GROUP BY
    l.CategoryId,
    c.Name,
    c.SortOrder
ORDER BY
    c.SortOrder,
    c.Name
