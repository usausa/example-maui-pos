SELECT
    TaxRateId,
    Rate,
    TaxIncluded,
    TaxableAmount,
    TaxAmount
FROM
    DailyClosingTaxes
WHERE
    DailyClosingId = /*@ dailyClosingId */''
ORDER BY
    LineNo
