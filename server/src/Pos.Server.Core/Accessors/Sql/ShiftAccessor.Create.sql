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
