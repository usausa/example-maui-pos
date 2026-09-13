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
