SELECT
    p.PaymentMethodId,
    m.Name,
    m.Kind,
    COALESCE(SUM(CASE WHEN t.Type = 'Sale' THEN p.Amount ELSE 0 END), 0) AS SalesAmount,
    COUNT(DISTINCT CASE WHEN t.Type = 'Sale' THEN t.Id END) AS SalesCount,
    COALESCE(SUM(CASE WHEN t.Type = 'Return' THEN p.Amount ELSE 0 END), 0) AS ReturnAmount,
    COUNT(DISTINCT CASE WHEN t.Type = 'Return' THEN t.Id END) AS ReturnCount
FROM TransactionPayments p
JOIN Transactions t ON t.Id = p.TransactionId
JOIN PaymentMethods m ON m.Id = p.PaymentMethodId
WHERE t.ShiftId = /*@ shiftId */'' AND t.Status = 'Completed'
GROUP BY p.PaymentMethodId, m.Name, m.Kind, m.SortOrder
ORDER BY m.SortOrder, m.Name
