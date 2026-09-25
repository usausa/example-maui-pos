DELETE FROM
    TerminalTokens
WHERE
    TerminalId = /*@ terminalId */''
    AND TokenHash IS NULL
