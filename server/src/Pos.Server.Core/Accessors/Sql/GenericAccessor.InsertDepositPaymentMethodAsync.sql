INSERT INTO
    PaymentMethods
    (Id, Code, Name, ShortName, Kind, AllowsChange, RequiresReference, IsActive, SortOrder, IsDeleted, CreatedAt, UpdatedAt, Version)
SELECT
    '00000000-0000-0000-0008-000000000007',
    'DEPOSIT',
    '前受金',
    '前受金',
    'Deposit',
    0,
    0,
    1,
    7,
    0,
    /*@ now */'',
    /*@ now */'',
    1
WHERE
    NOT EXISTS (
        SELECT
            1
        FROM
            PaymentMethods
        WHERE
            Kind = 'Deposit'
            OR Code = 'DEPOSIT'
            OR Id = '00000000-0000-0000-0008-000000000007'
    )
