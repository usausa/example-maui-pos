SELECT
    *
FROM
    Products
WHERE
    Barcode = /*@ barcode */''
    AND IsDeleted = 0
