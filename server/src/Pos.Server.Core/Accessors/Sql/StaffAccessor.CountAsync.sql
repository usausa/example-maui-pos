SELECT COUNT(*) FROM Staff
WHERE 1 = 1
/*% if (storeId != null) { */
  AND (StoreId = /*@ storeId */'' OR StoreId IS NULL)
/*% } */
/*% if (updatedSince != null) { */
  AND UpdatedAt > /*@ updatedSince */''
/*% } */
/*% if (!includeDeleted) { */
  AND IsDeleted = 0
/*% } */
