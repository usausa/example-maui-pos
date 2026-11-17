namespace Pos.Server.Host.Models.Export;

using CsvHelper;

// 商品 CSV の 1 行 (GET /products/csv。取込の ProductImportRow と同じ見出し)
[CsvRecord]
public sealed partial class ProductExportRow
{
    [CsvColumn(ProductCsvHeader.Code)]
    public string Code { get; set; } = default!;

    [CsvColumn(ProductCsvHeader.Barcode)]
    public string? Barcode { get; set; }

    [CsvColumn(ProductCsvHeader.Name)]
    public string Name { get; set; } = default!;

    [CsvColumn(ProductCsvHeader.Kana)]
    public string? Kana { get; set; }

    [CsvColumn(ProductCsvHeader.Brand)]
    public string? Brand { get; set; }

    [CsvColumn(ProductCsvHeader.ModelNo)]
    public string? ModelNo { get; set; }

    [CsvColumn(ProductCsvHeader.CategoryCode)]
    public string CategoryCode { get; set; } = default!;

    [CsvColumn(ProductCsvHeader.CategoryName)]
    public string CategoryName { get; set; } = default!;

    [CsvColumn(ProductCsvHeader.Kind)]
    public ProductKind Kind { get; set; }

    [CsvColumn(ProductCsvHeader.Price)]
    public decimal Price { get; set; }

    [CsvColumn(ProductCsvHeader.TaxIncluded)]
    public bool TaxIncluded { get; set; }

    [CsvColumn(ProductCsvHeader.TaxRateCode)]
    public string TaxRateCode { get; set; } = default!;

    [CsvColumn(ProductCsvHeader.Cost)]
    public decimal? Cost { get; set; }

    [CsvColumn(ProductCsvHeader.PointRate)]
    public decimal PointRate { get; set; }

    [CsvColumn(ProductCsvHeader.RequiresSerial)]
    public bool RequiresSerial { get; set; }

    [CsvColumn(ProductCsvHeader.TrackInventory)]
    public bool TrackInventory { get; set; }

    [CsvColumn(ProductCsvHeader.AllowsPriceOverride)]
    public bool AllowsPriceOverride { get; set; }

    [CsvColumn(ProductCsvHeader.Unit)]
    public string? Unit { get; set; }

    [CsvColumn(ProductCsvHeader.IsActive)]
    public bool IsActive { get; set; }
}
