SELECT
    s.TaxRateId,
    s.Rate,
    s.TaxIncluded,
    COALESCE(SUM(CASE WHEN t.Type = 'Sale' THEN s.TaxableAmount ELSE -s.TaxableAmount END), 0) AS TaxableAmount,
    COALESCE(SUM(CASE WHEN t.Type = 'Sale' THEN s.TaxAmount ELSE -s.TaxAmount END), 0) AS TaxAmount
FROM
    TransactionTaxSummaries s
    JOIN Transactions t ON t.Id = s.TransactionId
WHERE
    t.ShiftId = /*@ shiftId */''
    AND t.Status = 'Completed'
GROUP BY
    s.TaxRateId,
    s.Rate,
    s.TaxIncluded
ORDER BY
    s.Rate DESC,
    s.TaxIncluded DESC
