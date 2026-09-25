INSERT INTO
    PurchaseOrders
    (Id, StoreId, Seq, PurchaseOrderNo, SupplierId, Status, ExpectedDate, Note, CreatedAt, UpdatedAt, Version)
SELECT
    /*@ id */'',
    /*@ storeId */'',
    COALESCE(MAX(Seq), 0) + 1,
    /*@ storeCode */'' || '-P-' || printf('%06d', COALESCE(MAX(Seq), 0) + 1),
    /*@ supplierId */'',
    'Draft',
    /*@ expectedDate */'',
    /*@ note */'',
    /*@ now */'',
    /*@ now */'',
    1
FROM
    PurchaseOrders
WHERE
    StoreId = /*@ storeId */''
RETURNING
    *
