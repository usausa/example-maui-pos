SELECT * FROM Shifts WHERE TerminalId = /*@ terminalId */'' AND Status = 'Open' ORDER BY OpenedAt DESC LIMIT 1
