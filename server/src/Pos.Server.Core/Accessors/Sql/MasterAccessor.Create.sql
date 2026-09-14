CREATE TABLE IF NOT EXISTS Settings (
    Id                    INTEGER  NOT NULL,
    CompanyName           TEXT     NOT NULL,
    Currency              TEXT     NOT NULL,
    TaxRounding           TEXT     NOT NULL,
    PointBasis            TEXT     NOT NULL,
    BusinessDayStartTime  TEXT     NOT NULL,
    UpdatedAt             TEXT     NOT NULL,
    Version               INTEGER  NOT NULL,
    PRIMARY KEY (Id)
);

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

CREATE TABLE IF NOT EXISTS Staff (
    Id         TEXT     NOT NULL,
    Code       TEXT     NOT NULL,
    Name       TEXT     NOT NULL,
    Role       TEXT     NOT NULL,
    StoreId    TEXT,
    PinHash    BLOB,
    IsActive   INTEGER  NOT NULL,
    IsDeleted  INTEGER  NOT NULL,
    CreatedAt  TEXT     NOT NULL,
    UpdatedAt  TEXT     NOT NULL,
    Version    INTEGER  NOT NULL,
    PRIMARY KEY (Id),
    UNIQUE (Code),
    FOREIGN KEY (StoreId) REFERENCES Stores (Id)
);
CREATE INDEX IF NOT EXISTS IX_Staff_StoreId ON Staff (StoreId);
CREATE INDEX IF NOT EXISTS IX_Staff_UpdatedAt ON Staff (UpdatedAt);

CREATE TABLE IF NOT EXISTS Categories (
    Id         TEXT     NOT NULL,
    Code       TEXT     NOT NULL,
    Name       TEXT     NOT NULL,
    ParentId   TEXT,
    SortOrder  INTEGER  NOT NULL,
    IsDeleted  INTEGER  NOT NULL,
    CreatedAt  TEXT     NOT NULL,
    UpdatedAt  TEXT     NOT NULL,
    Version    INTEGER  NOT NULL,
    PRIMARY KEY (Id),
    UNIQUE (Code),
    FOREIGN KEY (ParentId) REFERENCES Categories (Id)
);
CREATE INDEX IF NOT EXISTS IX_Categories_ParentId ON Categories (ParentId);
CREATE INDEX IF NOT EXISTS IX_Categories_UpdatedAt ON Categories (UpdatedAt);

CREATE TABLE IF NOT EXISTS TaxRates (
    Id         TEXT     NOT NULL,
    Code       TEXT     NOT NULL,
    Name       TEXT     NOT NULL,
    Rate       NUMERIC  NOT NULL,
    Kind       TEXT     NOT NULL,
    IsDefault  INTEGER  NOT NULL,
    SortOrder  INTEGER  NOT NULL,
    IsDeleted  INTEGER  NOT NULL,
    CreatedAt  TEXT     NOT NULL,
    UpdatedAt  TEXT     NOT NULL,
    Version    INTEGER  NOT NULL,
    PRIMARY KEY (Id),
    UNIQUE (Code)
);
CREATE INDEX IF NOT EXISTS IX_TaxRates_UpdatedAt ON TaxRates (UpdatedAt);

CREATE TABLE IF NOT EXISTS Discounts (
    Id                TEXT     NOT NULL,
    Code              TEXT     NOT NULL,
    Name              TEXT     NOT NULL,
    Type              TEXT     NOT NULL,
    Value             NUMERIC  NOT NULL,
    Scope             TEXT     NOT NULL,
    RequiresApproval  INTEGER  NOT NULL,
    IsActive          INTEGER  NOT NULL,
    SortOrder         INTEGER  NOT NULL,
    IsDeleted         INTEGER  NOT NULL,
    CreatedAt         TEXT     NOT NULL,
    UpdatedAt         TEXT     NOT NULL,
    Version           INTEGER  NOT NULL,
    PRIMARY KEY (Id),
    UNIQUE (Code)
);
CREATE INDEX IF NOT EXISTS IX_Discounts_UpdatedAt ON Discounts (UpdatedAt);

CREATE TABLE IF NOT EXISTS PaymentMethods (
    Id                 TEXT     NOT NULL,
    Code               TEXT     NOT NULL,
    Name               TEXT     NOT NULL,
    ShortName          TEXT,
    Kind               TEXT     NOT NULL,
    AllowsChange       INTEGER  NOT NULL,
    RequiresReference  INTEGER  NOT NULL,
    IsActive           INTEGER  NOT NULL,
    SortOrder          INTEGER  NOT NULL,
    IsDeleted          INTEGER  NOT NULL,
    CreatedAt          TEXT     NOT NULL,
    UpdatedAt          TEXT     NOT NULL,
    Version            INTEGER  NOT NULL,
    PRIMARY KEY (Id),
    UNIQUE (Code)
);
CREATE INDEX IF NOT EXISTS IX_PaymentMethods_UpdatedAt ON PaymentMethods (UpdatedAt);

CREATE TABLE IF NOT EXISTS AdjustmentReasons (
    Id         TEXT     NOT NULL,
    Code       TEXT     NOT NULL,
    Name       TEXT     NOT NULL,
    SortOrder  INTEGER  NOT NULL,
    IsActive   INTEGER  NOT NULL,
    IsDeleted  INTEGER  NOT NULL,
    CreatedAt  TEXT     NOT NULL,
    UpdatedAt  TEXT     NOT NULL,
    Version    INTEGER  NOT NULL,
    PRIMARY KEY (Id),
    UNIQUE (Code)
);
CREATE INDEX IF NOT EXISTS IX_AdjustmentReasons_UpdatedAt ON AdjustmentReasons (UpdatedAt);
