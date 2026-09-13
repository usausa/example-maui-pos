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
