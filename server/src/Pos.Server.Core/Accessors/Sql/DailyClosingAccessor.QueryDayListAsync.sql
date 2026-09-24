SELECT
    d.StoreId AS StoreId,
    d.BusinessDate AS BusinessDate,
    CASE WHEN c.Id IS NULL THEN 'Open' ELSE 'Closed' END AS Status,
    c.Id AS Id,
    COALESCE(c.ShiftCount, s.ShiftCount, 0) AS ShiftCount,
    COALESCE(s.OpenShiftCount, 0) AS OpenShiftCount,
    COALESCE(c.SalesCount, t.SalesCount, 0) AS SalesCount,
    COALESCE(c.ReturnCount, t.ReturnCount, 0) AS ReturnCount,
    COALESCE(c.VoidCount, t.VoidCount, 0) AS VoidCount,
    COALESCE(c.CustomerCount, t.CustomerCount, 0) AS CustomerCount,
    COALESCE(c.SalesTotal, t.SalesTotal, 0) AS SalesTotal,
    COALESCE(c.ReturnsTotal, t.ReturnsTotal, 0) AS ReturnsTotal,
    COALESCE(c.NetSales, t.NetSales, 0) AS NetSales,
    COALESCE(c.DiscountTotal, t.DiscountTotal, 0) AS DiscountTotal,
    COALESCE(c.TaxTotal, t.TaxTotal, 0) AS TaxTotal,
    COALESCE(c.PointsEarned, t.PointsEarned, 0) AS PointsEarned,
    COALESCE(c.PointsRedeemed, t.PointsRedeemed, 0) AS PointsRedeemed,
    COALESCE(c.HasLateTransactions, 0) AS HasLateTransactions,
    c.ClosedAt AS ClosedAt,
    c.ClosedBy AS ClosedBy
FROM
    (
        SELECT
            StoreId,
            BusinessDate
        FROM
            Shifts
        WHERE
            1 = 1
/*% if (storeId != null) { */
            AND StoreId = /*@ storeId */''
/*% } */
/*% if (from != null) { */
            AND BusinessDate >= /*@ from */''
/*% } */
/*% if (to != null) { */
            AND BusinessDate <= /*@ to */''
/*% } */
        UNION
        SELECT
            StoreId,
            BusinessDate
        FROM
            Transactions
        WHERE
            1 = 1
/*% if (storeId != null) { */
            AND StoreId = /*@ storeId */''
/*% } */
/*% if (from != null) { */
            AND BusinessDate >= /*@ from */''
/*% } */
/*% if (to != null) { */
            AND BusinessDate <= /*@ to */''
/*% } */
        UNION
        SELECT
            StoreId,
            BusinessDate
        FROM
            DailyClosings
        WHERE
            1 = 1
/*% if (storeId != null) { */
            AND StoreId = /*@ storeId */''
/*% } */
/*% if (from != null) { */
            AND BusinessDate >= /*@ from */''
/*% } */
/*% if (to != null) { */
            AND BusinessDate <= /*@ to */''
/*% } */
    ) d
    LEFT JOIN DailyClosings c ON c.StoreId = d.StoreId AND c.BusinessDate = d.BusinessDate
    LEFT JOIN (
        SELECT
            x.StoreId,
            x.BusinessDate,
            COUNT(*) AS ShiftCount,
            SUM(CASE WHEN sh.Status = 'Open' THEN 1 ELSE 0 END) AS OpenShiftCount
        FROM
            (
                SELECT
                    StoreId,
                    BusinessDate,
                    Id AS ShiftId
                FROM
                    Shifts
                WHERE
                    1 = 1
/*% if (storeId != null) { */
                    AND StoreId = /*@ storeId */''
/*% } */
/*% if (from != null) { */
                    AND BusinessDate >= /*@ from */''
/*% } */
/*% if (to != null) { */
                    AND BusinessDate <= /*@ to */''
/*% } */
                UNION
                SELECT
                    StoreId,
                    BusinessDate,
                    ShiftId
                FROM
                    Transactions
                WHERE
                    1 = 1
/*% if (storeId != null) { */
                    AND StoreId = /*@ storeId */''
/*% } */
/*% if (from != null) { */
                    AND BusinessDate >= /*@ from */''
/*% } */
/*% if (to != null) { */
                    AND BusinessDate <= /*@ to */''
/*% } */
            ) x
            JOIN Shifts sh ON sh.Id = x.ShiftId
        GROUP BY
            x.StoreId,
            x.BusinessDate
    ) s ON s.StoreId = d.StoreId AND s.BusinessDate = d.BusinessDate
    LEFT JOIN (
        SELECT
            StoreId,
            BusinessDate,
            SUM(CASE WHEN Status = 'Completed' AND Type = 'Sale' THEN 1 ELSE 0 END) AS SalesCount,
            SUM(CASE WHEN Status = 'Completed' AND Type = 'Return' THEN 1 ELSE 0 END) AS ReturnCount,
            SUM(CASE WHEN Status = 'Voided' THEN 1 ELSE 0 END) AS VoidCount,
            COUNT(DISTINCT CASE WHEN Status = 'Completed' THEN CustomerId END) AS CustomerCount,
            SUM(CASE WHEN Status = 'Completed' AND Type = 'Sale' THEN Total ELSE 0 END) AS SalesTotal,
            SUM(CASE WHEN Status = 'Completed' AND Type = 'Return' THEN Total ELSE 0 END) AS ReturnsTotal,
            SUM(CASE WHEN Status <> 'Completed' THEN 0 WHEN Type = 'Sale' THEN Total ELSE -Total END) AS NetSales,
            SUM(CASE WHEN Status <> 'Completed' THEN 0 WHEN Type = 'Sale' THEN DiscountTotal ELSE -DiscountTotal END) AS DiscountTotal,
            SUM(CASE WHEN Status <> 'Completed' THEN 0 WHEN Type = 'Sale' THEN TaxTotal ELSE -TaxTotal END) AS TaxTotal,
            SUM(CASE WHEN Status = 'Completed' THEN PointsEarned ELSE 0 END) AS PointsEarned,
            SUM(CASE WHEN Status = 'Completed' THEN PointsRedeemed ELSE 0 END) AS PointsRedeemed
        FROM
            Transactions
        WHERE
            1 = 1
/*% if (storeId != null) { */
            AND StoreId = /*@ storeId */''
/*% } */
/*% if (from != null) { */
            AND BusinessDate >= /*@ from */''
/*% } */
/*% if (to != null) { */
            AND BusinessDate <= /*@ to */''
/*% } */
        GROUP BY
            StoreId,
            BusinessDate
    ) t ON t.StoreId = d.StoreId AND t.BusinessDate = d.BusinessDate
    LEFT JOIN Stores st ON st.Id = d.StoreId
WHERE
    1 = 1
/*% if (status != null) { */
    AND (CASE WHEN c.Id IS NULL THEN 'Open' ELSE 'Closed' END) = /*@ status */''
/*% } */
ORDER BY
/*% if (desc) { */
    /*# sort */BusinessDate DESC,
/*% } else { */
    /*# sort */BusinessDate,
/*% } */
    st.Code
LIMIT /*@ limit */20 OFFSET /*@ offset */0
