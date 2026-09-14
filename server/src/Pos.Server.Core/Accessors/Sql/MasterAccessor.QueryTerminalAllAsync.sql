SELECT * FROM Terminals
WHERE 1 = 1
/*% if (!includeDeleted) { */
  AND IsDeleted = 0
/*% } */
ORDER BY StoreId, TerminalNo
