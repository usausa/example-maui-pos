DELETE FROM
    Outbox
WHERE
    Status = 'Sent'
    AND SentAt < /*@ before */0
