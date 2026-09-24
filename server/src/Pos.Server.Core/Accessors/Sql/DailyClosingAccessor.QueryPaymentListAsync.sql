SELECT
    PaymentMethodId,
    Name,
    Kind,
    SalesAmount,
    SalesCount,
    ReturnAmount,
    ReturnCount
FROM
    DailyClosingPayments
WHERE
    DailyClosingId = /*@ dailyClosingId */''
ORDER BY
    LineNo
