CREATE TABLE IF NOT EXISTS Stores (
    Id              TEXT     NOT NULL,
    Code            TEXT     NOT NULL,
    Name            TEXT     NOT NULL,
    PostalCode      TEXT,
    Address         TEXT,
    Phone           TEXT,
    RegistrationNo  TEXT,
    ReceiptHeader   TEXT,
    ReceiptFooter   TEXT,
    TimeZone        TEXT     NOT NULL,
    IsActive        INTEGER  NOT NULL,
    IsDeleted       INTEGER  NOT NULL,
    CreatedAt       TEXT     NOT NULL,
    UpdatedAt       TEXT     NOT NULL,
    Version         INTEGER  NOT NULL,
    PRIMARY KEY (Id),
    UNIQUE (Code)
);
CREATE INDEX IF NOT EXISTS IX_Stores_UpdatedAt ON Stores (UpdatedAt);
