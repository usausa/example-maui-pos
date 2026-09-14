SELECT * FROM Categories
WHERE 1 = 1
/*% if (!includeDeleted) { */
  AND IsDeleted = 0
/*% } */
ORDER BY SortOrder, Code
