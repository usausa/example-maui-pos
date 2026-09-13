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
