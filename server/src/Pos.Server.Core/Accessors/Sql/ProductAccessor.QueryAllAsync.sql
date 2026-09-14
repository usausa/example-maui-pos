SELECT * FROM Products
WHERE 1 = 1
/*% if (!includeDeleted) { */
  AND IsDeleted = 0
/*% } */
ORDER BY Code
