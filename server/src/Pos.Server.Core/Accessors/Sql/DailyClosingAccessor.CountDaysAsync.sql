SELECT
    COUNT(*)
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
WHERE
    1 = 1
/*% if (status != null) { */
    AND (CASE WHEN c.Id IS NULL THEN 'Open' ELSE 'Closed' END) = /*@ status */''
/*% } */
