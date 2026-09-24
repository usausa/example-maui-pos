namespace Pos.Server.Host.Models.Export;

using CsvHelper.Configuration.Attributes;

// 商品 CSV の 1 行 (GET /products/csv。取込の ProductImportRow と同じ見出し)
public sealed class ProductExportRow
{
    [Name(ProductCsvHeader.Code)]
    public string Code { get; set; } = default!;

    [Name(ProductCsvHeader.Barcode)]
    public string? Barcode { get; set; }

    [Name(ProductCsvHeader.Name)]
    public string Name { get; set; } = default!;

    [Name(ProductCsvHeader.Kana)]
    public string? Kana { get; set; }

    [Name(ProductCsvHeader.Brand)]
    public string? Brand { get; set; }

    [Name(ProductCsvHeader.ModelNo)]
    public string? ModelNo { get; set; }

    [Name(ProductCsvHeader.CategoryCode)]
    public string CategoryCode { get; set; } = default!;

    [Name(ProductCsvHeader.CategoryName)]
    public string CategoryName { get; set; } = default!;

    [Name(ProductCsvHeader.Kind)]
    public ProductKind Kind { get; set; }

    [Name(ProductCsvHeader.Price)]
    public decimal Price { get; set; }

    [Name(ProductCsvHeader.TaxIncluded)]
    public bool TaxIncluded { get; set; }

    [Name(ProductCsvHeader.TaxRateCode)]
    public string TaxRateCode { get; set; } = default!;

    [Name(ProductCsvHeader.Cost)]
    public decimal? Cost { get; set; }

    [Name(ProductCsvHeader.PointRate)]
    public decimal PointRate { get; set; }

    [Name(ProductCsvHeader.RequiresSerial)]
    public bool RequiresSerial { get; set; }

    [Name(ProductCsvHeader.TrackInventory)]
    public bool TrackInventory { get; set; }

    [Name(ProductCsvHeader.AllowsPriceOverride)]
    public bool AllowsPriceOverride { get; set; }

    [Name(ProductCsvHeader.Unit)]
    public string? Unit { get; set; }

    [Name(ProductCsvHeader.IsActive)]
    public bool IsActive { get; set; }
}
