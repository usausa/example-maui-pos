-- スキーマ (起動時に GenericAccessor.ExecuteSchemaAsync で実行する。CREATE TABLE IF NOT EXISTS なので何度実行してもよい)

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

CREATE TABLE IF NOT EXISTS Customers (
    Id            TEXT     NOT NULL,
    Code          TEXT     NOT NULL,
    Name          TEXT     NOT NULL,
    Kana          TEXT,
    Phone         TEXT,
    Email         TEXT,
    PostalCode    TEXT,
    Address       TEXT,
    BirthDate     TEXT,
    PointBalance  INTEGER  NOT NULL,
    Note          TEXT,
    IsDeleted     INTEGER  NOT NULL,
    CreatedAt     TEXT     NOT NULL,
    UpdatedAt     TEXT     NOT NULL,
    Version       INTEGER  NOT NULL,
    PRIMARY KEY (Id),
    UNIQUE (Code)
);
CREATE INDEX IF NOT EXISTS IX_Customers_Phone ON Customers (Phone);
CREATE INDEX IF NOT EXISTS IX_Customers_Kana ON Customers (Kana);
CREATE INDEX IF NOT EXISTS IX_Customers_UpdatedAt ON Customers (UpdatedAt);

CREATE TABLE IF NOT EXISTS PointHistories (
    Id             TEXT     NOT NULL,
    CustomerId     TEXT     NOT NULL,
    Type           TEXT     NOT NULL,
    Points         INTEGER  NOT NULL,
    BalanceAfter   INTEGER  NOT NULL,
    TransactionId  TEXT,
    Reason         TEXT,
    StaffId        TEXT,
    OccurredAt     TEXT     NOT NULL,
    CreatedAt      TEXT     NOT NULL,
    PRIMARY KEY (Id),
    FOREIGN KEY (CustomerId) REFERENCES Customers (Id),
    FOREIGN KEY (TransactionId) REFERENCES Transactions (Id),
    FOREIGN KEY (StaffId) REFERENCES Staff (Id)
);
CREATE INDEX IF NOT EXISTS IX_PointHistories_CustomerId_OccurredAt ON PointHistories (CustomerId, OccurredAt DESC);
CREATE INDEX IF NOT EXISTS IX_PointHistories_TransactionId ON PointHistories (TransactionId);

CREATE TABLE IF NOT EXISTS Shifts (
    Id               TEXT     NOT NULL,
    StoreId          TEXT     NOT NULL,
    TerminalId       TEXT     NOT NULL,
    Status           TEXT     NOT NULL,   -- Open / Closed
    BusinessDate     TEXT     NOT NULL,   -- yyyy-MM-dd
    OpenedAt         TEXT     NOT NULL,
    OpenedByStaffId  TEXT     NOT NULL,
    OpeningCash      NUMERIC  NOT NULL,
    ClosedAt         TEXT,
    ClosedByStaffId  TEXT,
    ActualCash       NUMERIC,
    ExpectedCash     NUMERIC,
    Difference       NUMERIC,
    CashSales        NUMERIC  NOT NULL DEFAULT 0,
    CashReturns      NUMERIC  NOT NULL DEFAULT 0,
    PaidIn           NUMERIC  NOT NULL DEFAULT 0,
    PaidOut          NUMERIC  NOT NULL DEFAULT 0,
    SalesCount       INTEGER  NOT NULL DEFAULT 0,
    ReturnCount      INTEGER  NOT NULL DEFAULT 0,
    VoidCount        INTEGER  NOT NULL DEFAULT 0,
    SalesTotal       NUMERIC  NOT NULL DEFAULT 0,
    ReturnsTotal     NUMERIC  NOT NULL DEFAULT 0,
    Note             TEXT,
    CreatedAt        TEXT     NOT NULL,
    UpdatedAt        TEXT     NOT NULL,
    PRIMARY KEY (Id),
    FOREIGN KEY (StoreId) REFERENCES Stores (Id),
    FOREIGN KEY (TerminalId) REFERENCES Terminals (Id),
    FOREIGN KEY (OpenedByStaffId) REFERENCES Staff (Id),
    FOREIGN KEY (ClosedByStaffId) REFERENCES Staff (Id)
);
CREATE UNIQUE INDEX IF NOT EXISTS UX_Shifts_Open ON Shifts (TerminalId) WHERE Status = 'Open';
CREATE INDEX IF NOT EXISTS IX_Shifts_StoreId_BusinessDate ON Shifts (StoreId, BusinessDate);

CREATE TABLE IF NOT EXISTS ShiftDenominations (
    ShiftId       TEXT     NOT NULL,
    Denomination  INTEGER  NOT NULL,
    Count         INTEGER  NOT NULL,
    PRIMARY KEY (ShiftId, Denomination),
    FOREIGN KEY (ShiftId) REFERENCES Shifts (Id)
);

CREATE TABLE IF NOT EXISTS CashEvents (
    Id          TEXT     NOT NULL,
    ShiftId     TEXT     NOT NULL,
    Type        TEXT     NOT NULL,   -- PaidIn / PaidOut / NoSale
    Amount      NUMERIC  NOT NULL,
    Reason      TEXT,
    StaffId     TEXT     NOT NULL,
    OccurredAt  TEXT     NOT NULL,
    CreatedAt   TEXT     NOT NULL,
    PRIMARY KEY (Id),
    FOREIGN KEY (ShiftId) REFERENCES Shifts (Id),
    FOREIGN KEY (StaffId) REFERENCES Staff (Id)
);
CREATE INDEX IF NOT EXISTS IX_CashEvents_ShiftId_OccurredAt ON CashEvents (ShiftId, OccurredAt);

CREATE TABLE IF NOT EXISTS Transactions (
    Id                     TEXT     NOT NULL,
    Type                   TEXT     NOT NULL,   -- Sale / Return
    Status                 TEXT     NOT NULL,   -- Completed / Voided
    StoreId                TEXT     NOT NULL,
    TerminalId             TEXT     NOT NULL,
    StaffId                TEXT     NOT NULL,
    ShiftId                TEXT     NOT NULL,
    CustomerId             TEXT,
    ReceiptNo              TEXT     NOT NULL,
    BusinessDate           TEXT     NOT NULL,   -- yyyy-MM-dd
    TransactedAt           TEXT     NOT NULL,   -- UTC
    OriginalTransactionId  TEXT,
    Subtotal               NUMERIC  NOT NULL,
    DiscountTotal          NUMERIC  NOT NULL,
    NetSubtotal            NUMERIC  NOT NULL,
    TaxTotal               NUMERIC  NOT NULL,
    Total                  NUMERIC  NOT NULL,
    TenderedTotal          NUMERIC  NOT NULL,
    ChangeAmount           NUMERIC  NOT NULL,
    PointsEarned           INTEGER  NOT NULL,
    PointsRedeemed         INTEGER  NOT NULL,
    PointsBalanceAfter     INTEGER,
    Note                   TEXT,
    VoidedAt               TEXT,
    VoidedByStaffId        TEXT,
    VoidReason             TEXT,
    CreatedAt              TEXT     NOT NULL,
    UpdatedAt              TEXT     NOT NULL,
    PRIMARY KEY (Id),
    UNIQUE (ReceiptNo),
    FOREIGN KEY (StoreId) REFERENCES Stores (Id),
    FOREIGN KEY (TerminalId) REFERENCES Terminals (Id),
    FOREIGN KEY (StaffId) REFERENCES Staff (Id),
    FOREIGN KEY (ShiftId) REFERENCES Shifts (Id),
    FOREIGN KEY (CustomerId) REFERENCES Customers (Id),
    FOREIGN KEY (OriginalTransactionId) REFERENCES Transactions (Id)
);
CREATE INDEX IF NOT EXISTS IX_Transactions_StoreId_BusinessDate ON Transactions (StoreId, BusinessDate);
CREATE INDEX IF NOT EXISTS IX_Transactions_ShiftId ON Transactions (ShiftId);
CREATE INDEX IF NOT EXISTS IX_Transactions_TerminalId_TransactedAt ON Transactions (TerminalId, TransactedAt);
CREATE INDEX IF NOT EXISTS IX_Transactions_CustomerId_TransactedAt ON Transactions (CustomerId, TransactedAt);
CREATE INDEX IF NOT EXISTS IX_Transactions_OriginalTransactionId ON Transactions (OriginalTransactionId);
CREATE INDEX IF NOT EXISTS IX_Transactions_TransactedAt ON Transactions (TransactedAt);

CREATE TABLE IF NOT EXISTS TransactionLines (
    Id                       TEXT     NOT NULL,
    TransactionId            TEXT     NOT NULL,
    LineNo                   INTEGER  NOT NULL,
    ProductId                TEXT     NOT NULL,
    ProductCode              TEXT     NOT NULL,
    ProductName              TEXT     NOT NULL,
    CategoryId               TEXT     NOT NULL,
    Kind                     TEXT     NOT NULL,   -- Goods / Service
    ListPrice                NUMERIC  NOT NULL,
    UnitPrice                NUMERIC  NOT NULL,
    Quantity                 NUMERIC  NOT NULL,
    TaxRateId                TEXT     NOT NULL,
    TaxRate                  NUMERIC  NOT NULL,
    TaxIncluded              INTEGER  NOT NULL,
    PointRate                NUMERIC  NOT NULL,
    Amount                   NUMERIC  NOT NULL,
    DiscountAmount           NUMERIC  NOT NULL,
    AllocatedDiscountAmount  NUMERIC  NOT NULL,
    NetAmount                NUMERIC  NOT NULL,
    PointsRedeemed           INTEGER  NOT NULL,
    PointsEarned             INTEGER  NOT NULL,
    OriginalLineId           TEXT,
    ReturnedQuantity         NUMERIC  NOT NULL DEFAULT 0,
    Note                     TEXT,
    PRIMARY KEY (Id),
    UNIQUE (TransactionId, LineNo),
    FOREIGN KEY (TransactionId) REFERENCES Transactions (Id),
    FOREIGN KEY (ProductId) REFERENCES Products (Id),
    FOREIGN KEY (OriginalLineId) REFERENCES TransactionLines (Id)
);
CREATE INDEX IF NOT EXISTS IX_TransactionLines_ProductId ON TransactionLines (ProductId);
CREATE INDEX IF NOT EXISTS IX_TransactionLines_OriginalLineId ON TransactionLines (OriginalLineId);

CREATE TABLE IF NOT EXISTS TransactionLineSerials (
    TransactionLineId  TEXT  NOT NULL,
    SerialNumber       TEXT  NOT NULL,
    PRIMARY KEY (TransactionLineId, SerialNumber),
    FOREIGN KEY (TransactionLineId) REFERENCES TransactionLines (Id)
);
CREATE INDEX IF NOT EXISTS IX_TransactionLineSerials_SerialNumber ON TransactionLineSerials (SerialNumber);

CREATE TABLE IF NOT EXISTS TransactionDiscounts (
    Id                 TEXT     NOT NULL,
    TransactionId      TEXT     NOT NULL,
    LineId             TEXT,
    DiscountId         TEXT,
    SortNo             INTEGER  NOT NULL,
    Name               TEXT     NOT NULL,
    Type               TEXT     NOT NULL,   -- Amount / Percent
    Value              NUMERIC  NOT NULL,
    Amount             NUMERIC  NOT NULL,
    Reason             TEXT,
    ApprovedByStaffId  TEXT,
    PRIMARY KEY (Id),
    FOREIGN KEY (TransactionId) REFERENCES Transactions (Id),
    FOREIGN KEY (LineId) REFERENCES TransactionLines (Id),
    FOREIGN KEY (DiscountId) REFERENCES Discounts (Id),
    FOREIGN KEY (ApprovedByStaffId) REFERENCES Staff (Id)
);
CREATE INDEX IF NOT EXISTS IX_TransactionDiscounts_TransactionId ON TransactionDiscounts (TransactionId);

CREATE TABLE IF NOT EXISTS TransactionTaxSummaries (
    TransactionId  TEXT     NOT NULL,
    TaxRateId      TEXT     NOT NULL,
    TaxIncluded    INTEGER  NOT NULL,
    Rate           NUMERIC  NOT NULL,
    TaxableAmount  NUMERIC  NOT NULL,
    TaxAmount      NUMERIC  NOT NULL,
    PRIMARY KEY (TransactionId, TaxRateId, TaxIncluded),
    FOREIGN KEY (TransactionId) REFERENCES Transactions (Id),
    FOREIGN KEY (TaxRateId) REFERENCES TaxRates (Id)
);

CREATE TABLE IF NOT EXISTS TransactionPayments (
    Id               TEXT     NOT NULL,
    TransactionId    TEXT     NOT NULL,
    SeqNo            INTEGER  NOT NULL,
    PaymentMethodId  TEXT     NOT NULL,
    Kind             TEXT     NOT NULL,
    Amount           NUMERIC  NOT NULL,
    TenderedAmount   NUMERIC  NOT NULL,
    Reference        TEXT,
    Note             TEXT,
    PRIMARY KEY (Id),
    UNIQUE (TransactionId, SeqNo),
    FOREIGN KEY (TransactionId) REFERENCES Transactions (Id),
    FOREIGN KEY (PaymentMethodId) REFERENCES PaymentMethods (Id)
);
CREATE INDEX IF NOT EXISTS IX_TransactionPayments_PaymentMethodId ON TransactionPayments (PaymentMethodId);

CREATE TABLE IF NOT EXISTS TransactionDeliveries (
    TransactionId  TEXT  NOT NULL,
    RecipientName  TEXT  NOT NULL,
    Phone          TEXT,
    PostalCode     TEXT,
    Address        TEXT  NOT NULL,
    RequestedDate  TEXT,
    TimeSlot       TEXT,
    Note           TEXT,
    PRIMARY KEY (TransactionId),
    FOREIGN KEY (TransactionId) REFERENCES Transactions (Id)
);

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
