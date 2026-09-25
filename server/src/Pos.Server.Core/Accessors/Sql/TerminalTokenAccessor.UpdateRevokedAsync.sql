UPDATE
    TerminalTokens
SET
    RevokedAt = /*@ revokedAt */''
WHERE
    TerminalId = /*@ terminalId */''
    AND TokenHash IS NOT NULL
    AND RevokedAt IS NULL
