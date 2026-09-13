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
