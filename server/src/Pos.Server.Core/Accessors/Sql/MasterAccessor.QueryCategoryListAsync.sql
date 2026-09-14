SELECT * FROM Categories
WHERE 1 = 1
/*% if (updatedSince != null) { */
  AND UpdatedAt > /*@ updatedSince */''
/*% } */
/*% if (!includeDeleted) { */
  AND IsDeleted = 0
/*% } */
ORDER BY /*# sort */Id
LIMIT /*@ limit */20 OFFSET /*@ offset */0
