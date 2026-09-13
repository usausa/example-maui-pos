namespace Pos.Server.Models.Entity;

public sealed class ProductEntity
{
    [Key]
    public Guid Id { get; set; }

    public string Code { get; set; } = default!;

    public string? Barcode { get; set; }

    public string Name { get; set; } = default!;

    public string? Kana { get; set; }

    public string? Brand { get; set; }

    public string? ModelNo { get; set; }

    public Guid CategoryId { get; set; }

    public ProductKind Kind { get; set; }

    public decimal Price { get; set; }

    public bool TaxIncluded { get; set; }

    public Guid TaxRateId { get; set; }

    public decimal? Cost { get; set; }

    public decimal PointRate { get; set; }

    public bool RequiresSerial { get; set; }

    public bool TrackInventory { get; set; }

    public bool AllowsPriceOverride { get; set; }

    public string? Unit { get; set; }

#pragma warning disable CA1056
    public string? ImageUrl { get; set; }
#pragma warning restore CA1056

    public bool IsActive { get; set; }

    public bool IsDeleted { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public int Version { get; set; }
}
