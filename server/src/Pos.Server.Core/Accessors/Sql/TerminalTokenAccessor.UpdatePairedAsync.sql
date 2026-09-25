UPDATE
    TerminalTokens
SET
    PairingCode = NULL,
    TokenHash = /*@ tokenHash */NULL,
    DeviceName = /*@ deviceName */'',
    PairedAt = /*@ pairedAt */''
WHERE
    Id = /*@ id */''
    AND TokenHash IS NULL
