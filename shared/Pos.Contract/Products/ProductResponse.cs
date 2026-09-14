namespace Pos.Contract.Products;

using Pos.Contract;

public sealed class ProductResponseItem
{
    public Guid Id { get; set; }

    public string Code { get; set; } = default!;

    // JAN / EAN
    public string? Barcode { get; set; }

    public string Name { get; set; } = default!;

    public string? Kana { get; set; }

    public string? Brand { get; set; }

    public string? ModelNo { get; set; }

    public Guid CategoryId { get; set; }

    public ProductKind Kind { get; set; }

    public decimal Price { get; set; }

    // price が税込か (内税 = true / 外税 = false)
    public bool TaxIncluded { get; set; }

    public Guid TaxRateId { get; set; }

    public decimal? Cost { get; set; }

    // 0.10 = 10%、0 = 対象外
    public decimal PointRate { get; set; }

    public bool RequiresSerial { get; set; }

    public bool TrackInventory { get; set; }

    public bool AllowsPriceOverride { get; set; }

    public string? Unit { get; set; }

    public string? ImageUrl { get; set; }

    public bool IsActive { get; set; }

    public bool IsDeleted { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public int Version { get; set; }
}

public sealed class ProductResponse : ListResponse<ProductResponseItem>;
