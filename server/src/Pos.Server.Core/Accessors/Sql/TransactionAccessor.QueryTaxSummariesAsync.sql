SELECT
    *
FROM
    TransactionTaxSummaries
WHERE
    TransactionId = /*@ transactionId */''
ORDER BY
    Rate DESC,
    TaxIncluded DESC
