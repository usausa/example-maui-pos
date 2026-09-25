SELECT
    TerminalId,
    DeviceName,
    PairedAt,
    RevokedAt
FROM
    (
        SELECT
            TerminalId,
            DeviceName,
            PairedAt,
            RevokedAt,
            ROW_NUMBER() OVER (PARTITION BY TerminalId ORDER BY PairedAt DESC) AS RowNo
        FROM
            TerminalTokens
        WHERE
            PairedAt IS NOT NULL
    )
WHERE
    RowNo = 1
