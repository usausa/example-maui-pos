namespace Pos.Server.Host.Models.Import;

using CsvHelper;

using Pos.Server.Host.Infrastructure.Csv;
using Pos.Server.Host.Models.Export;
using Pos.Server.Models.Parameters;

using Smart.Mapper;

// 商品 CSV の取込の 1 行。見出しは出力 (ProductExportRow) と同じで、参照用の「部門」は読まない。
// 値は文字列のまま読み、変換と検証は ProductService.ImportAsync が行う (行ごとに誤りを返すため)
[CsvRecord]
public sealed partial class ProductImportRow
{
    [CsvColumn(ProductCsvHeader.Code)]
    public string? Code { get; set; }

    [CsvColumn(ProductCsvHeader.Barcode)]
    public string? Barcode { get; set; }

    [CsvColumn(ProductCsvHeader.Name)]
    public string? Name { get; set; }

    [CsvColumn(ProductCsvHeader.Kana)]
    public string? Kana { get; set; }

    [CsvColumn(ProductCsvHeader.Brand)]
    public string? Brand { get; set; }

    [CsvColumn(ProductCsvHeader.ModelNo)]
    public string? ModelNo { get; set; }

    [CsvColumn(ProductCsvHeader.CategoryCode)]
    public string? CategoryCode { get; set; }

    [CsvColumn(ProductCsvHeader.Kind)]
    public string? Kind { get; set; }

    [CsvColumn(ProductCsvHeader.Price)]
    public string? Price { get; set; }

    [CsvColumn(ProductCsvHeader.TaxIncluded)]
    public string? TaxIncluded { get; set; }

    [CsvColumn(ProductCsvHeader.TaxRateCode)]
    public string? TaxRateCode { get; set; }

    [CsvColumn(ProductCsvHeader.Cost)]
    public string? Cost { get; set; }

    [CsvColumn(ProductCsvHeader.PointRate)]
    public string? PointRate { get; set; }

    [CsvColumn(ProductCsvHeader.RequiresSerial)]
    public string? RequiresSerial { get; set; }

    [CsvColumn(ProductCsvHeader.TrackInventory)]
    public string? TrackInventory { get; set; }

    [CsvColumn(ProductCsvHeader.AllowsPriceOverride)]
    public string? AllowsPriceOverride { get; set; }

    [CsvColumn(ProductCsvHeader.Unit)]
    public string? Unit { get; set; }

    [CsvColumn(ProductCsvHeader.IsActive)]
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
