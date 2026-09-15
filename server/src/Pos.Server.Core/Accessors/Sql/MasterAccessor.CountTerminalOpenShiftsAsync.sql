SELECT
    COUNT(*)
FROM
    Shifts
WHERE
    TerminalId = /*@ terminalId */''
    AND Status = 'Open'
