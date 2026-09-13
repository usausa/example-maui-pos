CREATE TABLE IF NOT EXISTS Products (
    Id                   TEXT     NOT NULL,
    Code                 TEXT     NOT NULL,
    Barcode              TEXT,
    Name                 TEXT     NOT NULL,
    Kana                 TEXT,
    Brand                TEXT,
    ModelNo              TEXT,
    CategoryId           TEXT     NOT NULL,
    Kind                 TEXT     NOT NULL,
    Price                NUMERIC  NOT NULL,
    TaxIncluded          INTEGER  NOT NULL,
    TaxRateId            TEXT     NOT NULL,
    Cost                 NUMERIC,
    PointRate            NUMERIC  NOT NULL,
    RequiresSerial       INTEGER  NOT NULL,
    TrackInventory       INTEGER  NOT NULL,
    AllowsPriceOverride  INTEGER  NOT NULL,
    Unit                 TEXT,
    ImageUrl             TEXT,
    IsActive             INTEGER  NOT NULL,
    IsDeleted            INTEGER  NOT NULL,
    CreatedAt            TEXT     NOT NULL,
    UpdatedAt            TEXT     NOT NULL,
    Version              INTEGER  NOT NULL,
    PRIMARY KEY (Id),
    UNIQUE (Code),
    FOREIGN KEY (CategoryId) REFERENCES Categories (Id),
    FOREIGN KEY (TaxRateId) REFERENCES TaxRates (Id)
);
CREATE INDEX IF NOT EXISTS IX_Products_CategoryId ON Products (CategoryId);
CREATE INDEX IF NOT EXISTS IX_Products_Name ON Products (Name);
CREATE INDEX IF NOT EXISTS IX_Products_Kana ON Products (Kana);
CREATE INDEX IF NOT EXISTS IX_Products_UpdatedAt ON Products (UpdatedAt);
CREATE UNIQUE INDEX IF NOT EXISTS UX_Products_Barcode ON Products (Barcode) WHERE Barcode IS NOT NULL;
