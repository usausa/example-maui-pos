SELECT
    p.Id,
    p.Code,
    p.Barcode,
    p.Name,
    p.Kana,
    p.Brand,
    p.ModelNo,
    c.Code AS CategoryCode,
    c.Name AS CategoryName,
    p.Kind,
    p.Price,
    p.TaxIncluded,
    t.Code AS TaxRateCode,
    p.Cost,
    p.PointRate,
    p.RequiresSerial,
    p.TrackInventory,
    p.AllowsPriceOverride,
    p.Unit,
    p.IsActive
FROM
    Products p
    JOIN Categories c ON c.Id = p.CategoryId
    JOIN TaxRates t ON t.Id = p.TaxRateId
WHERE
    p.IsDeleted = 0
ORDER BY
    p.Code
