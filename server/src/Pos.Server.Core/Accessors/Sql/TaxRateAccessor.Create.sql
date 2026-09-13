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
