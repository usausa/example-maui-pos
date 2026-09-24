namespace Pos.Server.Models.Enums;

// 商品 CSV の取込の列 (見出しの文言は Host が付ける)
public enum ProductImportColumn
{
    Code,
    Barcode,
    Name,
    Kana,
    Brand,
    ModelNo,
    CategoryCode,
    Kind,
    Price,
    TaxIncluded,
    TaxRateCode,
    Cost,
    PointRate,
    RequiresSerial,
    TrackInventory,
    AllowsPriceOverride,
    Unit,
    IsActive
}
