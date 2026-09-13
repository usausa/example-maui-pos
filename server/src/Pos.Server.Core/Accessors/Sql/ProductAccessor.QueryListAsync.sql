SELECT * FROM Products
WHERE 1 = 1
/*% if (categoryId != null) { */
  AND CategoryId = /*@ categoryId */''
/*% } */
/*% if (keyword != null) { */
  AND (Code LIKE /*@ keyword */'' ESCAPE '\' OR Barcode LIKE /*@ keyword */'' ESCAPE '\' OR Name LIKE /*@ keyword */'' ESCAPE '\' OR Kana LIKE /*@ keyword */'' ESCAPE '\' OR ModelNo LIKE /*@ keyword */'' ESCAPE '\')
/*% } */
/*% if (isActive != null) { */
  AND IsActive = /*@ isActive */1
/*% } */
/*% if (updatedSince != null) { */
  AND UpdatedAt > /*@ updatedSince */''
/*% } */
/*% if (!includeDeleted) { */
  AND IsDeleted = 0
/*% } */
ORDER BY /*# sort */Id
LIMIT /*@ limit */20 OFFSET /*@ offset */0
