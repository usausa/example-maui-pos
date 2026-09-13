namespace Pos.Server.Host.Models.Export;

using CsvHelper.Configuration.Attributes;

using Pos.Server.Models.Entity;

// 商品 CSV の 1 行 (GET /products/csv)
public sealed class ProductExportRow
{
    [Name("コード")]
    public string Code { get; set; } = default!;

    [Name("JAN")]
    public string? Barcode { get; set; }

    [Name("商品名")]
    public string Name { get; set; } = default!;

    [Name("かな")]
    public string? Kana { get; set; }

    [Name("メーカー")]
    public string? Brand { get; set; }

    [Name("型番")]
    public string? ModelNo { get; set; }

    [Name("部門コード")]
    public string CategoryCode { get; set; } = default!;

    [Name("部門")]
    public string CategoryName { get; set; } = default!;

    [Name("種別")]
    public ProductKind Kind { get; set; }

    [Name("価格")]
    public decimal Price { get; set; }

    [Name("内税")]
    public bool TaxIncluded { get; set; }

    [Name("税率コード")]
    public string TaxRateCode { get; set; } = default!;

    [Name("原価")]
    public decimal? Cost { get; set; }

    [Name("還元率")]
    public decimal PointRate { get; set; }

    [Name("シリアル要")]
    public bool RequiresSerial { get; set; }

    [Name("在庫管理")]
    public bool TrackInventory { get; set; }

    [Name("売価変更可")]
    public bool AllowsPriceOverride { get; set; }

    [Name("単位")]
    public string? Unit { get; set; }

    [Name("販売可")]
    public bool IsActive { get; set; }

    public static ProductExportRow From(ProductEntity entity, IReadOnlyDictionary<Guid, CategoryEntity> categories, IReadOnlyDictionary<Guid, TaxRateEntity> taxRates) => new()
    {
        Code = entity.Code,
        Barcode = entity.Barcode,
        Name = entity.Name,
        Kana = entity.Kana,
        Brand = entity.Brand,
        ModelNo = entity.ModelNo,
        CategoryCode = categories.TryGetValue(entity.CategoryId, out var category) ? category.Code : String.Empty,
        CategoryName = category?.Name ?? String.Empty,
        Kind = entity.Kind,
        Price = entity.Price,
        TaxIncluded = entity.TaxIncluded,
        TaxRateCode = taxRates.TryGetValue(entity.TaxRateId, out var taxRate) ? taxRate.Code : String.Empty,
        Cost = entity.Cost,
        PointRate = entity.PointRate,
        RequiresSerial = entity.RequiresSerial,
        TrackInventory = entity.TrackInventory,
        AllowsPriceOverride = entity.AllowsPriceOverride,
        Unit = entity.Unit,
        IsActive = entity.IsActive
    };
}
