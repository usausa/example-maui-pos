namespace Pos.Server.Host.Models.Import;

using CsvHelper.Configuration.Attributes;

using Pos.Server.Host.Infrastructure.Csv;
using Pos.Server.Host.Models.Export;
using Pos.Server.Models.Parameters;

using Smart.Mapper;

// 商品 CSV の取込の 1 行。見出しは出力 (ProductExportRow) と同じで、参照用の「部門」は読まない。
// 値は文字列のまま読み、変換と検証は ProductService.ImportAsync が行う (行ごとに誤りを返すため)
public sealed partial class ProductImportRow
{
    [Name(ProductCsvHeader.Code)]
    public string? Code { get; set; }

    [Name(ProductCsvHeader.Barcode)]
    public string? Barcode { get; set; }

    [Name(ProductCsvHeader.Name)]
    public string? Name { get; set; }

    [Name(ProductCsvHeader.Kana)]
    public string? Kana { get; set; }

    [Name(ProductCsvHeader.Brand)]
    public string? Brand { get; set; }

    [Name(ProductCsvHeader.ModelNo)]
    public string? ModelNo { get; set; }

    [Name(ProductCsvHeader.CategoryCode)]
    public string? CategoryCode { get; set; }

    [Name(ProductCsvHeader.Kind)]
    public string? Kind { get; set; }

    [Name(ProductCsvHeader.Price)]
    public string? Price { get; set; }

    [Name(ProductCsvHeader.TaxIncluded)]
    public string? TaxIncluded { get; set; }

    [Name(ProductCsvHeader.TaxRateCode)]
    public string? TaxRateCode { get; set; }

    [Name(ProductCsvHeader.Cost)]
    public string? Cost { get; set; }

    [Name(ProductCsvHeader.PointRate)]
    public string? PointRate { get; set; }

    [Name(ProductCsvHeader.RequiresSerial)]
    public string? RequiresSerial { get; set; }

    [Name(ProductCsvHeader.TrackInventory)]
    public string? TrackInventory { get; set; }

    [Name(ProductCsvHeader.AllowsPriceOverride)]
    public string? AllowsPriceOverride { get; set; }

    [Name(ProductCsvHeader.Unit)]
    public string? Unit { get; set; }

    [Name(ProductCsvHeader.IsActive)]
    public string? IsActive { get; set; }

    public static IReadOnlyList<ProductImportLine> ToLines(IEnumerable<CsvImportRow<ProductImportRow>> rows) =>
        rows.Select(static x =>
        {
            var line = ToLine(x.Record);
            line.LineNo = x.LineNo;
            return line;
        }).ToList();

    [Mapper]
    private static partial ProductImportLine ToLine(ProductImportRow row);
}
