namespace Pos.Server.Models.Views;

// 商品の CSV 出力用 (部門・税率のコードと名称付き)
public sealed class ProductExportItem
{
    public Guid Id { get; set; }

    public string Code { get; set; } = default!;

    public string? Barcode { get; set; }

    public string Name { get; set; } = default!;

    public string? Kana { get; set; }

    public string? Brand { get; set; }

    public string? ModelNo { get; set; }

    public string CategoryCode { get; set; } = default!;

    public string CategoryName { get; set; } = default!;

    public ProductKind Kind { get; set; }

    public decimal Price { get; set; }

    public bool TaxIncluded { get; set; }

    public string TaxRateCode { get; set; } = default!;

    public decimal? Cost { get; set; }

    public decimal PointRate { get; set; }

    public bool RequiresSerial { get; set; }

    public bool TrackInventory { get; set; }

    public bool AllowsPriceOverride { get; set; }

    public string? Unit { get; set; }

    public bool IsActive { get; set; }
}
