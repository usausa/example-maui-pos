SELECT * FROM Customers
WHERE PointBalance < 0 AND IsDeleted = 0
ORDER BY PointBalance, Code
LIMIT /*@ limit */20
