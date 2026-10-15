SELECT
    *
FROM
    Outbox
WHERE
    Status <> 'Sent'
ORDER BY
    CreatedAt
LIMIT /*@ limit */100
