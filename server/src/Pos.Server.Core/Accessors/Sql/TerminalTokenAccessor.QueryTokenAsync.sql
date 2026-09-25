SELECT
    tt.Id,
    tt.TerminalId,
    t.StoreId,
    t.Name AS TerminalName
FROM
    TerminalTokens tt
    INNER JOIN Terminals t ON t.Id = tt.TerminalId
WHERE
    tt.TokenHash = /*@ tokenHash */NULL
    AND tt.RevokedAt IS NULL
    AND t.IsActive = 1
    AND t.IsDeleted = 0
