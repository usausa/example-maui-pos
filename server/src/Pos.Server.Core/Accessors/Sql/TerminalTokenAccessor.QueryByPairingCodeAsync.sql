SELECT
    *
FROM
    TerminalTokens
WHERE
    PairingCode = /*@ pairingCode */''
    AND TokenHash IS NULL
