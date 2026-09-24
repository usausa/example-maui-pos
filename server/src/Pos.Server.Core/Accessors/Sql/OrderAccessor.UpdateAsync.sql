UPDATE
    Orders
SET
    CustomerId = /*@ customerId */'',
    CustomerName = /*@ customerName */'',
    Phone = /*@ phone */'',
    RequestedDate = /*@ requestedDate */'',
    Note = /*@ note */'',
    Total = /*@ total */0,
    UpdatedAt = /*@ updatedAt */'',
    Version = Version + 1
WHERE
    Id = /*@ id */''
    AND Version = /*@ version */0
    AND Status IN ('Ordered', 'Arrived')
RETURNING
    *
