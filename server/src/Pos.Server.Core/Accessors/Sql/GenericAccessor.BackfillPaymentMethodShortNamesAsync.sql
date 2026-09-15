UPDATE
    PaymentMethods
SET
    ShortName = CASE Code
        WHEN 'CASH' THEN '現金'
        WHEN 'CARD' THEN 'クレカ'
        WHEN 'QR' THEN 'QR'
        WHEN 'EMONEY' THEN '電子マネー'
        WHEN 'VOUCHER' THEN '商品券'
        WHEN 'POINT' THEN 'ポイント'
    END,
    UpdatedAt = /*@ now */'',
    Version = Version + 1
WHERE
    ShortName IS NULL
    AND Code IN ('CASH', 'CARD', 'QR', 'EMONEY', 'VOUCHER', 'POINT')
