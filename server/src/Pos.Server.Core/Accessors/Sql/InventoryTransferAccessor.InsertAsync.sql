INSERT INTO
    InventoryTransfers
    (Id, FromStoreId, Seq, TransferNo, ToStoreId, Status, Note, CreatedAt, UpdatedAt, Version)
SELECT
    /*@ id */'',
    /*@ fromStoreId */'',
    COALESCE(MAX(Seq), 0) + 1,
    /*@ fromStoreCode */'' || '-T-' || printf('%06d', COALESCE(MAX(Seq), 0) + 1),
    /*@ toStoreId */'',
    'Requested',
    /*@ note */'',
    /*@ now */'',
    /*@ now */'',
    1
FROM
    InventoryTransfers
WHERE
    FromStoreId = /*@ fromStoreId */''
RETURNING
    *
