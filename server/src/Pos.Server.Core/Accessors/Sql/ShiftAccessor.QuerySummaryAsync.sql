SELECT
    COALESCE(t.SalesCount, 0) AS SalesCount,
    COALESCE(t.ReturnCount, 0) AS ReturnCount,
    COALESCE(t.VoidCount, 0) AS VoidCount,
    COALESCE(t.SalesTotal, 0) AS SalesTotal,
    COALESCE(t.ReturnsTotal, 0) AS ReturnsTotal,
    COALESCE(p.CashSales, 0) AS CashSales,
    COALESCE(p.CashReturns, 0) AS CashReturns,
    COALESCE(c.PaidIn, 0) AS PaidIn,
    COALESCE(c.PaidOut, 0) AS PaidOut,
    COALESCE(d.DepositCashIn, 0) AS DepositCashIn,
    COALESCE(d.DepositCashOut, 0) AS DepositCashOut
FROM
    (
        SELECT
            SUM(CASE WHEN Status = 'Completed' AND Type = 'Sale' THEN 1 ELSE 0 END) AS SalesCount,
            SUM(CASE WHEN Status = 'Completed' AND Type = 'Return' THEN 1 ELSE 0 END) AS ReturnCount,
            SUM(CASE WHEN Status = 'Voided' THEN 1 ELSE 0 END) AS VoidCount,
            SUM(CASE WHEN Status = 'Completed' AND Type = 'Sale' THEN Total ELSE 0 END) AS SalesTotal,
            SUM(CASE WHEN Status = 'Completed' AND Type = 'Return' THEN Total ELSE 0 END) AS ReturnsTotal
        FROM
            Transactions
        WHERE
            ShiftId = /*@ shiftId */''
    ) t
    CROSS JOIN (
        SELECT
            SUM(CASE WHEN t.Type = 'Sale' THEN p.Amount ELSE 0 END) AS CashSales,
            SUM(CASE WHEN t.Type = 'Return' THEN p.Amount ELSE 0 END) AS CashReturns
        FROM
            TransactionPayments p
            JOIN Transactions t ON t.Id = p.TransactionId
        WHERE
            t.ShiftId = /*@ shiftId */''
            AND t.Status = 'Completed'
            AND p.Kind = 'Cash'
    ) p
    CROSS JOIN (
        SELECT
            SUM(CASE WHEN Type = 'PaidIn' THEN Amount ELSE 0 END) AS PaidIn,
            SUM(CASE WHEN Type = 'PaidOut' THEN Amount ELSE 0 END) AS PaidOut
        FROM
            CashEvents
        WHERE
            ShiftId = /*@ shiftId */''
    ) c
    CROSS JOIN (
        SELECT
            SUM(CASE WHEN Type = 'Receive' THEN Amount ELSE 0 END) AS DepositCashIn,
            SUM(CASE WHEN Type = 'Refund' THEN Amount ELSE 0 END) AS DepositCashOut
        FROM
            OrderDeposits
        WHERE
            ShiftId = /*@ shiftId */''
            AND Kind = 'Cash'
    ) d
