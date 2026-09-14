namespace Pos.Contract.Products;

public sealed class ProductUpdateRequest
{
    [Required]
    [MaxLength(20)]
    public string Code { get; set; } = default!;

    [MaxLength(20)]
    public string? Barcode { get; set; }

    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = default!;

    [MaxLength(100)]
    public string? Kana { get; set; }

    [MaxLength(50)]
    public string? Brand { get; set; }

    [MaxLength(50)]
    public string? ModelNo { get; set; }

    public Guid CategoryId { get; set; }

    public ProductKind Kind { get; set; }

    [Range(0, double.MaxValue)]
    public decimal Price { get; set; }

    public bool TaxIncluded { get; set; }

    public Guid TaxRateId { get; set; }

    [Range(0, double.MaxValue)]
    public decimal? Cost { get; set; }

    [Range(0, 1)]
    public decimal PointRate { get; set; }

    public bool RequiresSerial { get; set; }

    public bool TrackInventory { get; set; }

    public bool AllowsPriceOverride { get; set; }

    [MaxLength(10)]
    public string? Unit { get; set; }

    public bool IsActive { get; set; }

    public int Version { get; set; }
}
