SELECT s.* FROM TransactionLineSerials s
JOIN TransactionLines l ON l.Id = s.TransactionLineId
WHERE l.TransactionId = /*@ transactionId */''
ORDER BY l.LineNo, s.SerialNumber
