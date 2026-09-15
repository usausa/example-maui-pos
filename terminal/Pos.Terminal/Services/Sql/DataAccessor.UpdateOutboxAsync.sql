UPDATE
    Outbox
SET
    Status = /*@ status */'',
    Attempts = /*@ attempts */0,
    LastError = /*@ lastError */'',
    SentAt = /*@ sentAt */0
WHERE
    Id = /*@ id */''
