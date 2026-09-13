SELECT * FROM PointHistories
WHERE CustomerId = /*@ customerId */''
ORDER BY OccurredAt DESC, Id DESC
LIMIT /*@ limit */20 OFFSET /*@ offset */0
