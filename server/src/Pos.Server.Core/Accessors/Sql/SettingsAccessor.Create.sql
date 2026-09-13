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
