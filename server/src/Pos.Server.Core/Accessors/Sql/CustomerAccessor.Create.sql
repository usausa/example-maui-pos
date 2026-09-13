CREATE TABLE IF NOT EXISTS Customers (
    Id            TEXT     NOT NULL,
    Code          TEXT     NOT NULL,
    Name          TEXT     NOT NULL,
    Kana          TEXT,
    Phone         TEXT,
    Email         TEXT,
    PostalCode    TEXT,
    Address       TEXT,
    BirthDate     TEXT,
    PointBalance  INTEGER  NOT NULL,
    Note          TEXT,
    IsDeleted     INTEGER  NOT NULL,
    CreatedAt     TEXT     NOT NULL,
    UpdatedAt     TEXT     NOT NULL,
    Version       INTEGER  NOT NULL,
    PRIMARY KEY (Id),
    UNIQUE (Code)
);
CREATE INDEX IF NOT EXISTS IX_Customers_Phone ON Customers (Phone);
CREATE INDEX IF NOT EXISTS IX_Customers_Kana ON Customers (Kana);
CREATE INDEX IF NOT EXISTS IX_Customers_UpdatedAt ON Customers (UpdatedAt);

CREATE TABLE IF NOT EXISTS PointHistories (
    Id             TEXT     NOT NULL,
    CustomerId     TEXT     NOT NULL,
    Type           TEXT     NOT NULL,
    Points         INTEGER  NOT NULL,
    BalanceAfter   INTEGER  NOT NULL,
    TransactionId  TEXT,
    Reason         TEXT,
    StaffId        TEXT,
    OccurredAt     TEXT     NOT NULL,
    CreatedAt      TEXT     NOT NULL,
    PRIMARY KEY (Id),
    FOREIGN KEY (CustomerId) REFERENCES Customers (Id),
    FOREIGN KEY (TransactionId) REFERENCES Transactions (Id),
    FOREIGN KEY (StaffId) REFERENCES Staff (Id)
);
CREATE INDEX IF NOT EXISTS IX_PointHistories_CustomerId_OccurredAt ON PointHistories (CustomerId, OccurredAt DESC);
CREATE INDEX IF NOT EXISTS IX_PointHistories_TransactionId ON PointHistories (TransactionId);
