UPDATE
    DailyClosings
SET
    HasLateTransactions = 1,
    UpdatedAt = /*@ updatedAt */''
WHERE
    StoreId = /*@ storeId */''
    AND BusinessDate = /*@ businessDate */''
