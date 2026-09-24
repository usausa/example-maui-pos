namespace Pos.Server.Models.Parameters;

// 商品 CSV の 1 行。値はファイルの文字列のまま (変換と検証は ProductService.ImportAsync が行う)
public sealed class ProductImportLine
{
    // ファイルの行番号 (見出しが 1 行目)
    public int LineNo { get; set; }

    public string? Code { get; set; }

    public string? Barcode { get; set; }

    public string? Name { get; set; }

    public string? Kana { get; set; }

    public string? Brand { get; set; }

    public string? ModelNo { get; set; }

    public string? CategoryCode { get; set; }

    public string? Kind { get; set; }

    public string? Price { get; set; }

    public string? TaxIncluded { get; set; }

    public string? TaxRateCode { get; set; }

    public string? Cost { get; set; }

    public string? PointRate { get; set; }

    public string? RequiresSerial { get; set; }

    public string? TrackInventory { get; set; }

    public string? AllowsPriceOverride { get; set; }

    public string? Unit { get; set; }

    public string? IsActive { get; set; }
}
