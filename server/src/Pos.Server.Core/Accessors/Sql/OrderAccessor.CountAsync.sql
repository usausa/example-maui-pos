SELECT
    COUNT(*)
FROM
    Orders
WHERE
    1 = 1
/*% if (storeId != null) { */
    AND StoreId = /*@ storeId */''
/*% } */
/*% if (status != null) { */
    AND Status = /*@ status */''
/*% } */
/*% if (openOnly) { */
    AND Status IN ('Ordered', 'Arrived')
/*% } */
/*% if (type != null) { */
    AND Type = /*@ type */''
/*% } */
/*% if (customerId != null) { */
    AND CustomerId = /*@ customerId */''
/*% } */
/*% if (keyword != null) { */
    AND (
        OrderNo LIKE /*@ keyword */'' ESCAPE '\'
        OR CustomerName LIKE /*@ keyword */'' ESCAPE '\'
        OR Phone LIKE /*@ keyword */'' ESCAPE '\'
    )
/*% } */
/*% if (orderedFrom != null) { */
    AND OrderedAt >= /*@ orderedFrom */''
/*% } */
/*% if (orderedTo != null) { */
    AND OrderedAt < /*@ orderedTo */''
/*% } */
