CREATE TABLE IF NOT EXISTS InventoryLevels (
    StoreId    TEXT     NOT NULL,
    ProductId  TEXT     NOT NULL,
    Quantity   NUMERIC  NOT NULL,
    UpdatedAt  TEXT     NOT NULL,
    PRIMARY KEY (StoreId, ProductId),
    FOREIGN KEY (StoreId) REFERENCES Stores (Id),
    FOREIGN KEY (ProductId) REFERENCES Products (Id)
);
CREATE INDEX IF NOT EXISTS IX_InventoryLevels_ProductId ON InventoryLevels (ProductId);
CREATE INDEX IF NOT EXISTS IX_InventoryLevels_StoreId_UpdatedAt ON InventoryLevels (StoreId, UpdatedAt);

CREATE TABLE IF NOT EXISTS InventoryChanges (
    Id               TEXT     NOT NULL,
    StoreId          TEXT     NOT NULL,
    ProductId        TEXT     NOT NULL,
    Type             TEXT     NOT NULL,   -- Sale / Return / Void / PhysicalCount / Adjustment
    QuantityDelta    NUMERIC  NOT NULL,
    QuantityAfter    NUMERIC  NOT NULL,
    ReasonId         TEXT,
    Reason           TEXT,
    ReferenceType    TEXT,
    ReferenceId      TEXT,
    ReferenceLineId  TEXT,
    StaffId          TEXT,
    OccurredAt       TEXT     NOT NULL,
    CreatedAt        TEXT     NOT NULL,
    PRIMARY KEY (Id),
    FOREIGN KEY (StoreId) REFERENCES Stores (Id),
    FOREIGN KEY (ProductId) REFERENCES Products (Id),
    FOREIGN KEY (ReasonId) REFERENCES AdjustmentReasons (Id),
    FOREIGN KEY (StaffId) REFERENCES Staff (Id)
);
CREATE INDEX IF NOT EXISTS IX_InventoryChanges_StoreId_ProductId_OccurredAt ON InventoryChanges (StoreId, ProductId, OccurredAt DESC);
CREATE INDEX IF NOT EXISTS IX_InventoryChanges_ReferenceId ON InventoryChanges (ReferenceId);
