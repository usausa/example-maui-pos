CREATE TABLE IF NOT EXISTS Settings (
    CompanyName TEXT NOT NULL,
    Currency TEXT NOT NULL,
    TaxRounding TEXT NOT NULL,
    PointBasis TEXT NOT NULL,
    BusinessDayStartTime TEXT NOT NULL,
    UpdatedAt INTEGER NOT NULL,
    Version INTEGER NOT NULL
);

CREATE TABLE IF NOT EXISTS Stores (
    Id TEXT NOT NULL,
    Code TEXT NOT NULL,
    Name TEXT NOT NULL,
    PostalCode TEXT,
    Address TEXT,
    Phone TEXT,
    RegistrationNo TEXT,
    ReceiptHeader TEXT,
    ReceiptFooter TEXT,
    TimeZone TEXT NOT NULL,
    IsActive INTEGER NOT NULL,
    IsDeleted INTEGER NOT NULL,
    CreatedAt INTEGER NOT NULL,
    UpdatedAt INTEGER NOT NULL,
    Version INTEGER NOT NULL,
    PRIMARY KEY (Id)
);

CREATE TABLE IF NOT EXISTS Terminals (
    Id TEXT NOT NULL,
    StoreId TEXT NOT NULL,
    TerminalNo INTEGER NOT NULL,
    Name TEXT NOT NULL,
    LastReceiptSeq INTEGER NOT NULL,
    LastSeenAt INTEGER,
    AppVersion TEXT,
    IsActive INTEGER NOT NULL,
    IsDeleted INTEGER NOT NULL,
    CreatedAt INTEGER NOT NULL,
    UpdatedAt INTEGER NOT NULL,
    Version INTEGER NOT NULL,
    PRIMARY KEY (Id)
);

CREATE TABLE IF NOT EXISTS Staff (
    Id TEXT NOT NULL,
    Code TEXT NOT NULL,
    Name TEXT NOT NULL,
    Role TEXT NOT NULL,
    StoreId TEXT,
    IsActive INTEGER NOT NULL,
    IsDeleted INTEGER NOT NULL,
    CreatedAt INTEGER NOT NULL,
    UpdatedAt INTEGER NOT NULL,
    Version INTEGER NOT NULL,
    PRIMARY KEY (Id)
);

CREATE TABLE IF NOT EXISTS Categories (
    Id TEXT NOT NULL,
    Code TEXT NOT NULL,
    Name TEXT NOT NULL,
    ParentId TEXT,
    SortOrder INTEGER NOT NULL,
    IsDeleted INTEGER NOT NULL,
    CreatedAt INTEGER NOT NULL,
    UpdatedAt INTEGER NOT NULL,
    Version INTEGER NOT NULL,
    PRIMARY KEY (Id)
);

CREATE TABLE IF NOT EXISTS TaxRates (
    Id TEXT NOT NULL,
    Code TEXT NOT NULL,
    Name TEXT NOT NULL,
    Rate NUMERIC NOT NULL,
    Kind TEXT NOT NULL,
    IsDefault INTEGER NOT NULL,
    SortOrder INTEGER NOT NULL,
    IsDeleted INTEGER NOT NULL,
    CreatedAt INTEGER NOT NULL,
    UpdatedAt INTEGER NOT NULL,
    Version INTEGER NOT NULL,
    PRIMARY KEY (Id)
);

CREATE TABLE IF NOT EXISTS Products (
    Id TEXT NOT NULL,
    Code TEXT NOT NULL,
    Barcode TEXT,
    Name TEXT NOT NULL,
    Kana TEXT,
    Brand TEXT,
    ModelNo TEXT,
    CategoryId TEXT NOT NULL,
    Kind TEXT NOT NULL,
    Price NUMERIC NOT NULL,
    TaxIncluded INTEGER NOT NULL,
    TaxRateId TEXT NOT NULL,
    Cost NUMERIC,
    PointRate NUMERIC NOT NULL,
    RequiresSerial INTEGER NOT NULL,
    TrackInventory INTEGER NOT NULL,
    AllowsPriceOverride INTEGER NOT NULL,
    Unit TEXT,
    ImageUrl TEXT,
    IsActive INTEGER NOT NULL,
    IsDeleted INTEGER NOT NULL,
    CreatedAt INTEGER NOT NULL,
    UpdatedAt INTEGER NOT NULL,
    Version INTEGER NOT NULL,
    PRIMARY KEY (Id)
);
CREATE INDEX IF NOT EXISTS IX_Products_Barcode ON Products (Barcode);
CREATE INDEX IF NOT EXISTS IX_Products_Code ON Products (Code);

CREATE TABLE IF NOT EXISTS Discounts (
    Id TEXT NOT NULL,
    Code TEXT NOT NULL,
    Name TEXT NOT NULL,
    Type TEXT NOT NULL,
    Value NUMERIC NOT NULL,
    Scope TEXT NOT NULL,
    RequiresApproval INTEGER NOT NULL,
    IsActive INTEGER NOT NULL,
    SortOrder INTEGER NOT NULL,
    IsDeleted INTEGER NOT NULL,
    CreatedAt INTEGER NOT NULL,
    UpdatedAt INTEGER NOT NULL,
    Version INTEGER NOT NULL,
    PRIMARY KEY (Id)
);

CREATE TABLE IF NOT EXISTS PaymentMethods (
    Id TEXT NOT NULL,
    Code TEXT NOT NULL,
    Name TEXT NOT NULL,
    Kind TEXT NOT NULL,
    AllowsChange INTEGER NOT NULL,
    RequiresReference INTEGER NOT NULL,
    IsActive INTEGER NOT NULL,
    SortOrder INTEGER NOT NULL,
    IsDeleted INTEGER NOT NULL,
    CreatedAt INTEGER NOT NULL,
    UpdatedAt INTEGER NOT NULL,
    Version INTEGER NOT NULL,
    PRIMARY KEY (Id)
);

CREATE TABLE IF NOT EXISTS AdjustmentReasons (
    Id TEXT NOT NULL,
    Code TEXT NOT NULL,
    Name TEXT NOT NULL,
    SortOrder INTEGER NOT NULL,
    IsActive INTEGER NOT NULL,
    IsDeleted INTEGER NOT NULL,
    CreatedAt INTEGER NOT NULL,
    UpdatedAt INTEGER NOT NULL,
    Version INTEGER NOT NULL,
    PRIMARY KEY (Id)
);

CREATE TABLE IF NOT EXISTS InventoryLevels (
    StoreId TEXT NOT NULL,
    ProductId TEXT NOT NULL,
    Quantity NUMERIC NOT NULL,
    UpdatedAt INTEGER NOT NULL,
    PRIMARY KEY (StoreId, ProductId)
);

CREATE TABLE IF NOT EXISTS Transactions (
    Id TEXT NOT NULL,
    Type TEXT NOT NULL,
    Status TEXT NOT NULL,
    ShiftId TEXT NOT NULL,
    ReceiptNo TEXT NOT NULL,
    BusinessDate TEXT NOT NULL,
    TransactedAt INTEGER NOT NULL,
    CustomerId TEXT,
    Total NUMERIC NOT NULL,
    PointsEarned INTEGER NOT NULL,
    PointsRedeemed INTEGER NOT NULL,
    OriginalTransactionId TEXT,
    Payload TEXT NOT NULL,
    PRIMARY KEY (Id)
);
CREATE INDEX IF NOT EXISTS IX_Transactions_Shift ON Transactions (ShiftId, TransactedAt);
CREATE INDEX IF NOT EXISTS IX_Transactions_ReceiptNo ON Transactions (ReceiptNo);

CREATE TABLE IF NOT EXISTS Shifts (
    Id TEXT NOT NULL,
    StoreId TEXT NOT NULL,
    TerminalId TEXT NOT NULL,
    Status TEXT NOT NULL,
    BusinessDate TEXT NOT NULL,
    OpenedAt INTEGER NOT NULL,
    OpenedByStaffId TEXT NOT NULL,
    OpeningCash NUMERIC NOT NULL,
    ClosedAt INTEGER,
    ClosedByStaffId TEXT,
    ActualCash NUMERIC,
    ExpectedCash NUMERIC,
    Difference NUMERIC,
    Note TEXT,
    PRIMARY KEY (Id)
);

CREATE TABLE IF NOT EXISTS CashEvents (
    Id TEXT NOT NULL,
    ShiftId TEXT NOT NULL,
    Type TEXT NOT NULL,
    Amount NUMERIC NOT NULL,
    Reason TEXT,
    StaffId TEXT NOT NULL,
    OccurredAt INTEGER NOT NULL,
    PRIMARY KEY (Id)
);

CREATE TABLE IF NOT EXISTS Outbox (
    Id TEXT NOT NULL,
    Kind TEXT NOT NULL,
    TargetId TEXT NOT NULL,
    Payload TEXT NOT NULL,
    CreatedAt INTEGER NOT NULL,
    Status TEXT NOT NULL,
    Attempts INTEGER NOT NULL,
    LastError TEXT,
    SentAt INTEGER,
    PRIMARY KEY (Id)
);
CREATE INDEX IF NOT EXISTS IX_Outbox_Status ON Outbox (Status, CreatedAt);

CREATE TABLE IF NOT EXISTS SyncState (
    Key TEXT NOT NULL,
    Value TEXT NOT NULL,
    PRIMARY KEY (Key)
);

CREATE TABLE IF NOT EXISTS HoldCarts (
    Id TEXT NOT NULL,
    CreatedAt INTEGER NOT NULL,
    Summary TEXT NOT NULL,
    Total NUMERIC NOT NULL,
    Payload TEXT NOT NULL,
    PRIMARY KEY (Id)
)
