CREATE TABLE IF NOT EXISTS PaymentMethods (
    Id                 TEXT     NOT NULL,
    Code               TEXT     NOT NULL,
    Name               TEXT     NOT NULL,
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
