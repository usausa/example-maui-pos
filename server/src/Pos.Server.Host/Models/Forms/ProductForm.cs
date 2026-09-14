namespace Pos.Server.Host.Models.Forms;

using Pos.Server.Models.Entity;

using Smart.Mapper;

public sealed partial class ProductForm
{
    public Guid Id { get; set; }

    public string Code { get; set; } = string.Empty;

    public string? Barcode { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Kana { get; set; }

    public string? Brand { get; set; }

    public string? ModelNo { get; set; }

    public Guid? CategoryId { get; set; }

    public ProductKind Kind { get; set; } = ProductKind.Goods;

    public decimal Price { get; set; }

    public bool TaxIncluded { get; set; } = true;

    public Guid? TaxRateId { get; set; }

    public decimal? Cost { get; set; }

    // 0.01 = 1%
    public decimal PointRate { get; set; }

    public bool RequiresSerial { get; set; }

    public bool TrackInventory { get; set; } = true;

    public bool AllowsPriceOverride { get; set; }

    public string? Unit { get; set; }

    public bool IsActive { get; set; } = true;

    public int Version { get; set; }

    // Entity ↔ フォーム (サーバ付与項目はサービスが設定する)
    [Mapper]
    public static partial ProductForm ToForm(ProductEntity entity);

    [Mapper]
    [MapUsing(nameof(ProductEntity.CategoryId), nameof(ResolveCategoryId))]
    [MapUsing(nameof(ProductEntity.TaxRateId), nameof(ResolveTaxRateId))]
    public static partial ProductEntity ToEntity(ProductForm form);

    private static Guid ResolveCategoryId(ProductForm form) => form.CategoryId ?? Guid.Empty;

    private static Guid ResolveTaxRateId(ProductForm form) => form.TaxRateId ?? Guid.Empty;
}
