SELECT
    *
FROM
    Outbox
/*% if (status != null) { */
WHERE
    Status = /*@ status */''
/*% } else { */
WHERE
    Status <> 'Sent'
/*% } */
ORDER BY
    CreatedAt
LIMIT /*@ limit */100
