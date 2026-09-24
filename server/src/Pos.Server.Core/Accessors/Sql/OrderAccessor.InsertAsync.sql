INSERT INTO
    Orders
    (Id, StoreId, Seq, OrderNo, TerminalId, StaffId, CustomerId, CustomerName, Phone, Type, Status, RequestedDate, Note, Total, OrderedAt, ArrivedAt, CreatedAt, UpdatedAt, Version)
SELECT
    /*@ id */'',
    /*@ storeId */'',
    COALESCE(MAX(Seq), 0) + 1,
    /*@ storeCode */'' || '-O-' || printf('%06d', COALESCE(MAX(Seq), 0) + 1),
    /*@ terminalId */'',
    /*@ staffId */'',
    /*@ customerId */'',
    /*@ customerName */'',
    /*@ phone */'',
    /*@ type */'',
    /*@ status */'',
    /*@ requestedDate */'',
    /*@ note */'',
    /*@ total */0,
    /*@ orderedAt */'',
    /*@ arrivedAt */'',
    /*@ now */'',
    /*@ now */'',
    1
FROM
    Orders
WHERE
    StoreId = /*@ storeId */''
RETURNING
    *
