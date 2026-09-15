SELECT
    *
FROM
    Shifts
WHERE
    TerminalId = /*@ terminalId */''
    AND Status = 'Open'
