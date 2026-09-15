namespace Pos.Contract.Products;

public sealed class ProductCreateRequest
{
    [Required]
    [MaxLength(Length.Code)]
    public string Code { get; set; } = default!;

    [MaxLength(Length.Barcode)]
    public string? Barcode { get; set; }

    [Required]
    [MaxLength(Length.Name)]
    public string Name { get; set; } = default!;

    [MaxLength(Length.Kana)]
    public string? Kana { get; set; }

    [MaxLength(Length.Brand)]
    public string? Brand { get; set; }

    [MaxLength(Length.ModelNo)]
    public string? ModelNo { get; set; }

    public Guid CategoryId { get; set; }

    public ProductKind Kind { get; set; }

    [Range(0, double.MaxValue)]
    public decimal Price { get; set; }

    public bool TaxIncluded { get; set; } = true;

    public Guid TaxRateId { get; set; }

    [Range(0, double.MaxValue)]
    public decimal? Cost { get; set; }

    [Range(0, 1)]
    public decimal PointRate { get; set; }

    public bool RequiresSerial { get; set; }

    public bool TrackInventory { get; set; } = true;

    public bool AllowsPriceOverride { get; set; }

    [MaxLength(Length.Unit)]
    public string? Unit { get; set; }

    public bool IsActive { get; set; } = true;
}
