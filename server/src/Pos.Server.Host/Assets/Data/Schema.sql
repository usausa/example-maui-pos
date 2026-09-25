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

-- 端末の登録。ペアリングコードを発行した行が、ペアリングでトークン (のハッシュ) を持つ行になる
CREATE TABLE IF NOT EXISTS TerminalTokens (
    Id                TEXT     NOT NULL,
    TerminalId        TEXT     NOT NULL,
    PairingCode       TEXT,
    PairingExpiresAt  TEXT,
    TokenHash         BLOB,
    DeviceName        TEXT,
    PairedAt          TEXT,
    RevokedAt         TEXT,
    CreatedAt         TEXT     NOT NULL,
    PRIMARY KEY (Id),
    FOREIGN KEY (TerminalId) REFERENCES Terminals (Id)
);
CREATE INDEX IF NOT EXISTS IX_TerminalTokens_TerminalId ON TerminalTokens (TerminalId);
CREATE UNIQUE INDEX IF NOT EXISTS UX_TerminalTokens_TokenHash ON TerminalTokens (TokenHash) WHERE TokenHash IS NOT NULL;
CREATE INDEX IF NOT EXISTS IX_TerminalTokens_PairingCode ON TerminalTokens (PairingCode) WHERE PairingCode IS NOT NULL;

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

-- 管理画面のアカウント (端末には同期しない)
CREATE TABLE IF NOT EXISTS Accounts (
    Id           TEXT     NOT NULL,
    Name         TEXT     NOT NULL,
    Password     BLOB     NOT NULL,
    Role         TEXT     NOT NULL,
    IsActive     INTEGER  NOT NULL,
    LastLoginAt  TEXT,
    CreatedAt    TEXT     NOT NULL,
    UpdatedAt    TEXT     NOT NULL,
    Version      INTEGER  NOT NULL,
    PRIMARY KEY (Id),
    UNIQUE (Name)
);

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

-- 商品画像 (JPEG / PNG。形式は先頭のバイトで判定する)。Products.ImageUrl の v は Data のハッシュ
CREATE TABLE IF NOT EXISTS ProductImages (
    ProductId  TEXT  NOT NULL,
    Data       BLOB  NOT NULL,
    UpdatedAt  TEXT  NOT NULL,
    PRIMARY KEY (ProductId),
    FOREIGN KEY (ProductId) REFERENCES Products (Id)
);

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
    DepositCashIn    NUMERIC  NOT NULL DEFAULT 0,   -- 現金で受け取った前受金
    DepositCashOut   NUMERIC  NOT NULL DEFAULT 0,   -- 現金で返した前受金
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

-- 日次締め (店舗 × 営業日)。締めた時点の日計を持ち、締めを解除すると内訳ごと消す
CREATE TABLE IF NOT EXISTS DailyClosings (
    Id                   TEXT     NOT NULL,
    StoreId              TEXT     NOT NULL,
    BusinessDate         TEXT     NOT NULL,   -- yyyy-MM-dd
    ClosedAt             TEXT     NOT NULL,
    ClosedBy             TEXT,                -- 管理画面のアカウント名 (認証を無効にしているときは NULL)
    ShiftCount           INTEGER  NOT NULL,
    SalesCount           INTEGER  NOT NULL,
    ReturnCount          INTEGER  NOT NULL,
    VoidCount            INTEGER  NOT NULL,
    CustomerCount        INTEGER  NOT NULL,
    SalesTotal           NUMERIC  NOT NULL,
    ReturnsTotal         NUMERIC  NOT NULL,
    NetSales             NUMERIC  NOT NULL,
    DiscountTotal        NUMERIC  NOT NULL,
    TaxTotal             NUMERIC  NOT NULL,
    PointsEarned         INTEGER  NOT NULL,
    PointsRedeemed       INTEGER  NOT NULL,
    HasLateTransactions  INTEGER  NOT NULL DEFAULT 0,   -- 締め後に同じ営業日の取引が届いた
    CreatedAt            TEXT     NOT NULL,
    UpdatedAt            TEXT     NOT NULL,
    PRIMARY KEY (Id),
    FOREIGN KEY (StoreId) REFERENCES Stores (Id)
);
CREATE UNIQUE INDEX IF NOT EXISTS UX_DailyClosings_StoreId_BusinessDate ON DailyClosings (StoreId, BusinessDate);

CREATE TABLE IF NOT EXISTS DailyClosingPayments (
    DailyClosingId   TEXT     NOT NULL,
    LineNo           INTEGER  NOT NULL,
    PaymentMethodId  TEXT     NOT NULL,
    Name             TEXT     NOT NULL,
    Kind             TEXT     NOT NULL,
    SalesAmount      NUMERIC  NOT NULL,
    SalesCount       INTEGER  NOT NULL,
    ReturnAmount     NUMERIC  NOT NULL,
    ReturnCount      INTEGER  NOT NULL,
    PRIMARY KEY (DailyClosingId, LineNo),
    FOREIGN KEY (DailyClosingId) REFERENCES DailyClosings (Id),
    FOREIGN KEY (PaymentMethodId) REFERENCES PaymentMethods (Id)
);

CREATE TABLE IF NOT EXISTS DailyClosingTaxes (
    DailyClosingId  TEXT     NOT NULL,
    LineNo          INTEGER  NOT NULL,
    TaxRateId       TEXT     NOT NULL,
    Rate            NUMERIC  NOT NULL,
    TaxIncluded     INTEGER  NOT NULL,
    TaxableAmount   NUMERIC  NOT NULL,
    TaxAmount       NUMERIC  NOT NULL,
    PRIMARY KEY (DailyClosingId, LineNo),
    FOREIGN KEY (DailyClosingId) REFERENCES DailyClosings (Id),
    FOREIGN KEY (TaxRateId) REFERENCES TaxRates (Id)
);

-- 受注 (取り寄せ・取り置き)。受注番号は店舗ごとの連番 (Seq) から作る。会計した取引は TransactionId で持つ
CREATE TABLE IF NOT EXISTS Orders (
    Id             TEXT     NOT NULL,
    StoreId        TEXT     NOT NULL,
    Seq            INTEGER  NOT NULL,
    OrderNo        TEXT     NOT NULL,   -- {店舗コード}-O-{連番:000000}
    TerminalId     TEXT,
    StaffId        TEXT     NOT NULL,
    CustomerId     TEXT,
    CustomerName   TEXT     NOT NULL,
    Phone          TEXT,
    Type           TEXT     NOT NULL,   -- BackOrder / Hold
    Status         TEXT     NOT NULL,   -- Ordered / Arrived / Completed / Cancelled
    RequestedDate  TEXT,                -- yyyy-MM-dd
    Note           TEXT,
    Total          NUMERIC  NOT NULL,
    TransactionId  TEXT,
    OrderedAt      TEXT     NOT NULL,
    ArrivedAt      TEXT,
    CompletedAt    TEXT,
    CancelledAt    TEXT,
    CancelReason   TEXT,
    CreatedAt      TEXT     NOT NULL,
    UpdatedAt      TEXT     NOT NULL,
    Version        INTEGER  NOT NULL,
    PRIMARY KEY (Id),
    FOREIGN KEY (StoreId) REFERENCES Stores (Id),
    FOREIGN KEY (TerminalId) REFERENCES Terminals (Id),
    FOREIGN KEY (StaffId) REFERENCES Staff (Id),
    FOREIGN KEY (CustomerId) REFERENCES Customers (Id),
    FOREIGN KEY (TransactionId) REFERENCES Transactions (Id)
);
CREATE UNIQUE INDEX IF NOT EXISTS UX_Orders_StoreId_Seq ON Orders (StoreId, Seq);
CREATE UNIQUE INDEX IF NOT EXISTS UX_Orders_OrderNo ON Orders (OrderNo);
CREATE INDEX IF NOT EXISTS IX_Orders_StoreId_Status ON Orders (StoreId, Status);
CREATE INDEX IF NOT EXISTS IX_Orders_CustomerId ON Orders (CustomerId);
CREATE INDEX IF NOT EXISTS IX_Orders_TransactionId ON Orders (TransactionId);

CREATE TABLE IF NOT EXISTS OrderLines (
    Id           TEXT     NOT NULL,
    OrderId      TEXT     NOT NULL,
    LineNo       INTEGER  NOT NULL,
    ProductId    TEXT     NOT NULL,
    ProductCode  TEXT     NOT NULL,
    ProductName  TEXT     NOT NULL,
    Quantity     NUMERIC  NOT NULL,
    UnitPrice    NUMERIC  NOT NULL,
    Amount       NUMERIC  NOT NULL,
    Note         TEXT,
    PRIMARY KEY (Id),
    FOREIGN KEY (OrderId) REFERENCES Orders (Id),
    FOREIGN KEY (ProductId) REFERENCES Products (Id)
);
CREATE INDEX IF NOT EXISTS IX_OrderLines_OrderId ON OrderLines (OrderId);

-- 受注の前受金 (受取と返金)。受け取った店舗の端末とシフトで記録し、現金は精算の予想現金に入る。会計で充てた分は取引の支払 (Deposit) に残る
CREATE TABLE IF NOT EXISTS OrderDeposits (
    Id               TEXT     NOT NULL,   -- 端末が採番
    OrderId          TEXT     NOT NULL,
    StoreId          TEXT     NOT NULL,
    TerminalId       TEXT     NOT NULL,
    ShiftId          TEXT     NOT NULL,
    StaffId          TEXT     NOT NULL,
    Type             TEXT     NOT NULL,   -- Receive / Refund
    PaymentMethodId  TEXT     NOT NULL,
    Kind             TEXT     NOT NULL,
    Amount           NUMERIC  NOT NULL,
    Reference        TEXT,
    OccurredAt       TEXT     NOT NULL,
    CreatedAt        TEXT     NOT NULL,
    PRIMARY KEY (Id),
    FOREIGN KEY (OrderId) REFERENCES Orders (Id),
    FOREIGN KEY (StoreId) REFERENCES Stores (Id),
    FOREIGN KEY (TerminalId) REFERENCES Terminals (Id),
    FOREIGN KEY (ShiftId) REFERENCES Shifts (Id),
    FOREIGN KEY (StaffId) REFERENCES Staff (Id),
    FOREIGN KEY (PaymentMethodId) REFERENCES PaymentMethods (Id)
);
CREATE INDEX IF NOT EXISTS IX_OrderDeposits_OrderId ON OrderDeposits (OrderId);
CREATE INDEX IF NOT EXISTS IX_OrderDeposits_ShiftId ON OrderDeposits (ShiftId);

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

-- 仕入先 (入荷の相手)
CREATE TABLE IF NOT EXISTS Suppliers (
    Id         TEXT     NOT NULL,
    Code       TEXT     NOT NULL,
    Name       TEXT     NOT NULL,
    Phone      TEXT,
    Email      TEXT,
    Note       TEXT,
    IsActive   INTEGER  NOT NULL,
    IsDeleted  INTEGER  NOT NULL,
    CreatedAt  TEXT     NOT NULL,
    UpdatedAt  TEXT     NOT NULL,
    Version    INTEGER  NOT NULL,
    PRIMARY KEY (Id),
    UNIQUE (Code)
);

-- 入荷 (入荷予定を受領すると在庫に入る。明細の商品コード・名称は登録時点の写し)
CREATE TABLE IF NOT EXISTS InventoryReceipts (
    Id                 TEXT     NOT NULL,
    StoreId            TEXT     NOT NULL,
    SupplierId         TEXT     NOT NULL,
    SlipNo             TEXT,                -- 仕入先の納品書番号
    ExpectedDate       TEXT,                -- yyyy-MM-dd
    Status             TEXT     NOT NULL,   -- Draft / Received / Cancelled
    Note               TEXT,
    ReceivedAt         TEXT,
    ReceivedByStaffId  TEXT,
    CancelledAt        TEXT,
    CreatedAt          TEXT     NOT NULL,
    UpdatedAt          TEXT     NOT NULL,
    Version            INTEGER  NOT NULL,
    PRIMARY KEY (Id),
    FOREIGN KEY (StoreId) REFERENCES Stores (Id),
    FOREIGN KEY (SupplierId) REFERENCES Suppliers (Id),
    FOREIGN KEY (ReceivedByStaffId) REFERENCES Staff (Id)
);
CREATE INDEX IF NOT EXISTS IX_InventoryReceipts_StoreId_Status ON InventoryReceipts (StoreId, Status);

CREATE TABLE IF NOT EXISTS InventoryReceiptLines (
    Id                TEXT     NOT NULL,
    ReceiptId         TEXT     NOT NULL,
    LineNo            INTEGER  NOT NULL,
    ProductId         TEXT     NOT NULL,
    ProductCode       TEXT     NOT NULL,
    ProductName       TEXT     NOT NULL,
    Quantity          NUMERIC  NOT NULL,   -- 予定の数
    ReceivedQuantity  NUMERIC,             -- 受領した数 (受領まで NULL)
    Cost              NUMERIC,             -- 仕入単価
    PRIMARY KEY (Id),
    FOREIGN KEY (ReceiptId) REFERENCES InventoryReceipts (Id),
    FOREIGN KEY (ProductId) REFERENCES Products (Id)
);
CREATE INDEX IF NOT EXISTS IX_InventoryReceiptLines_ReceiptId ON InventoryReceiptLines (ReceiptId);

-- 発注 (仕入先への注文)。発注すると明細を写した入荷予定を作り、入荷予定の受領・キャンセルで入荷済み・キャンセルになる
CREATE TABLE IF NOT EXISTS PurchaseOrders (
    Id                TEXT     NOT NULL,
    StoreId           TEXT     NOT NULL,
    Seq               INTEGER  NOT NULL,
    PurchaseOrderNo   TEXT     NOT NULL,   -- {店舗コード}-P-{連番:000000}
    SupplierId        TEXT     NOT NULL,
    Status            TEXT     NOT NULL,   -- Draft / Ordered / Received / Cancelled
    ExpectedDate      TEXT,                -- yyyy-MM-dd (希望納期)
    Note              TEXT,
    OrderedAt         TEXT,
    OrderedBy         TEXT,                -- 発注した管理画面のアカウント名 (認証を無効にしているときは NULL)
    ReceiptId         TEXT,                -- 発注で作った入荷予定
    CancelledAt       TEXT,
    CreatedAt         TEXT     NOT NULL,
    UpdatedAt         TEXT     NOT NULL,
    Version           INTEGER  NOT NULL,
    PRIMARY KEY (Id),
    FOREIGN KEY (StoreId) REFERENCES Stores (Id),
    FOREIGN KEY (SupplierId) REFERENCES Suppliers (Id),
    FOREIGN KEY (ReceiptId) REFERENCES InventoryReceipts (Id)
);
CREATE UNIQUE INDEX IF NOT EXISTS UX_PurchaseOrders_StoreId_Seq ON PurchaseOrders (StoreId, Seq);
CREATE UNIQUE INDEX IF NOT EXISTS UX_PurchaseOrders_PurchaseOrderNo ON PurchaseOrders (PurchaseOrderNo);
CREATE INDEX IF NOT EXISTS IX_PurchaseOrders_StoreId_Status ON PurchaseOrders (StoreId, Status);
CREATE INDEX IF NOT EXISTS IX_PurchaseOrders_ReceiptId ON PurchaseOrders (ReceiptId);

CREATE TABLE IF NOT EXISTS PurchaseOrderLines (
    Id                TEXT     NOT NULL,
    PurchaseOrderId   TEXT     NOT NULL,
    LineNo            INTEGER  NOT NULL,   -- 入荷予定の明細と同じ番号
    ProductId         TEXT     NOT NULL,
    ProductCode       TEXT     NOT NULL,
    ProductName       TEXT     NOT NULL,
    Quantity          NUMERIC  NOT NULL,
    Cost              NUMERIC,             -- 仕入単価
    PRIMARY KEY (Id),
    FOREIGN KEY (PurchaseOrderId) REFERENCES PurchaseOrders (Id),
    FOREIGN KEY (ProductId) REFERENCES Products (Id)
);
CREATE INDEX IF NOT EXISTS IX_PurchaseOrderLines_PurchaseOrderId ON PurchaseOrderLines (PurchaseOrderId);

-- 店舗間移動 (出荷で出荷店の在庫が減り、受領で入荷店の在庫が増える)
CREATE TABLE IF NOT EXISTS InventoryTransfers (
    Id                 TEXT     NOT NULL,
    FromStoreId        TEXT     NOT NULL,
    Seq                INTEGER  NOT NULL,
    TransferNo         TEXT     NOT NULL,   -- {出荷店コード}-T-{連番:000000}
    ToStoreId          TEXT     NOT NULL,
    Status             TEXT     NOT NULL,   -- Requested / Shipped / Received / Cancelled
    Note               TEXT,
    ShippedAt          TEXT,
    ShippedByStaffId   TEXT,
    ReceivedAt         TEXT,
    ReceivedByStaffId  TEXT,
    CancelledAt        TEXT,
    CreatedAt          TEXT     NOT NULL,
    UpdatedAt          TEXT     NOT NULL,
    Version            INTEGER  NOT NULL,
    PRIMARY KEY (Id),
    FOREIGN KEY (FromStoreId) REFERENCES Stores (Id),
    FOREIGN KEY (ToStoreId) REFERENCES Stores (Id),
    FOREIGN KEY (ShippedByStaffId) REFERENCES Staff (Id),
    FOREIGN KEY (ReceivedByStaffId) REFERENCES Staff (Id)
);
CREATE UNIQUE INDEX IF NOT EXISTS UX_InventoryTransfers_FromStoreId_Seq ON InventoryTransfers (FromStoreId, Seq);
CREATE UNIQUE INDEX IF NOT EXISTS UX_InventoryTransfers_TransferNo ON InventoryTransfers (TransferNo);
CREATE INDEX IF NOT EXISTS IX_InventoryTransfers_ToStoreId_Status ON InventoryTransfers (ToStoreId, Status);

CREATE TABLE IF NOT EXISTS InventoryTransferLines (
    Id                TEXT     NOT NULL,
    TransferId        TEXT     NOT NULL,
    LineNo            INTEGER  NOT NULL,
    ProductId         TEXT     NOT NULL,
    ProductCode       TEXT     NOT NULL,
    ProductName       TEXT     NOT NULL,
    Quantity          NUMERIC  NOT NULL,   -- 依頼・出荷の数
    ReceivedQuantity  NUMERIC,             -- 受領した数 (受領まで NULL)
    PRIMARY KEY (Id),
    FOREIGN KEY (TransferId) REFERENCES InventoryTransfers (Id),
    FOREIGN KEY (ProductId) REFERENCES Products (Id)
);
CREATE INDEX IF NOT EXISTS IX_InventoryTransferLines_TransferId ON InventoryTransferLines (TransferId);

CREATE TABLE IF NOT EXISTS InventoryChanges (
    Id               TEXT     NOT NULL,
    StoreId          TEXT     NOT NULL,
    ProductId        TEXT     NOT NULL,
    Type             TEXT     NOT NULL,   -- Sale / Return / Void / PhysicalCount / Adjustment / Receive / TransferOut / TransferIn
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
