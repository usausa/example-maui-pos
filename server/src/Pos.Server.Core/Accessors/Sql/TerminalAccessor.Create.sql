CREATE TABLE IF NOT EXISTS Terminals (
    Id              TEXT     NOT NULL,
    StoreId         TEXT     NOT NULL,
    TerminalNo      INTEGER  NOT NULL,
    Name            TEXT     NOT NULL,
    LastReceiptSeq  INTEGER  NOT NULL,
    LastSeenAt      TEXT,
    AppVersion      TEXT,
    IsActive        INTEGER  NOT NULL,
    IsDeleted       INTEGER  NOT NULL,
    CreatedAt       TEXT     NOT NULL,
    UpdatedAt       TEXT     NOT NULL,
    Version         INTEGER  NOT NULL,
    PRIMARY KEY (Id),
    UNIQUE (StoreId, TerminalNo),
    FOREIGN KEY (StoreId) REFERENCES Stores (Id)
);
CREATE INDEX IF NOT EXISTS IX_Terminals_UpdatedAt ON Terminals (UpdatedAt);
