SELECT
    Status,
    COUNT(*) AS Count
FROM
    Orders
WHERE
    Status IN ('Ordered', 'Arrived')
/*% if (storeId != null) { */
    AND StoreId = /*@ storeId */''
/*% } */
GROUP BY
    Status
