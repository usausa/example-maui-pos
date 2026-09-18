SELECT
    COALESCE((SELECT SUM(p.Amount) FROM TransactionPayments p JOIN Transactions t ON t.Id = p.TransactionId
              WHERE t.ShiftId = /*@ shiftId */'' AND t.Status = 'Completed' AND t.Type = 'Sale' AND p.Kind = 'Cash'), 0) AS CashSales,
    COALESCE((SELECT SUM(p.Amount) FROM TransactionPayments p JOIN Transactions t ON t.Id = p.TransactionId
              WHERE t.ShiftId = /*@ shiftId */'' AND t.Status = 'Completed' AND t.Type = 'Return' AND p.Kind = 'Cash'), 0) AS CashReturns,
    COALESCE((SELECT SUM(Amount) FROM CashEvents WHERE ShiftId = /*@ shiftId */'' AND Type = 'PaidIn'), 0) AS PaidIn,
    COALESCE((SELECT SUM(Amount) FROM CashEvents WHERE ShiftId = /*@ shiftId */'' AND Type = 'PaidOut'), 0) AS PaidOut,
    (SELECT COUNT(*) FROM Transactions WHERE ShiftId = /*@ shiftId */'' AND Status = 'Completed' AND Type = 'Sale') AS SalesCount,
    (SELECT COUNT(*) FROM Transactions WHERE ShiftId = /*@ shiftId */'' AND Status = 'Completed' AND Type = 'Return') AS ReturnCount,
    (SELECT COUNT(*) FROM Transactions WHERE ShiftId = /*@ shiftId */'' AND Status = 'Voided') AS VoidCount,
    COALESCE((SELECT SUM(Total) FROM Transactions WHERE ShiftId = /*@ shiftId */'' AND Status = 'Completed' AND Type = 'Sale'), 0) AS SalesTotal,
    COALESCE((SELECT SUM(Total) FROM Transactions WHERE ShiftId = /*@ shiftId */'' AND Status = 'Completed' AND Type = 'Return'), 0) AS ReturnsTotal
