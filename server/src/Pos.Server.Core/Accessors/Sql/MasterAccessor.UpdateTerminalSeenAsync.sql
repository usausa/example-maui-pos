UPDATE
    Terminals
SET
    LastSeenAt = /*@ seenAt */'',
    AppVersion = COALESCE(/*@ appVersion */NULL, AppVersion)
WHERE
    Id = /*@ id */''
