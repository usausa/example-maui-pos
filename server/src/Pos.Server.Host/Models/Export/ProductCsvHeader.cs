namespace Pos.Server.Host.Models.Export;

// 商品 CSV の見出し (出力・取込・取込の誤りの文言で同じ名前を使う)
public static class ProductCsvHeader
{
    public const string Code = "コード";
    public const string Barcode = "JAN";
    public const string Name = "商品名";
    public const string Kana = "かな";
    public const string Brand = "メーカー";
    public const string ModelNo = "型番";
    public const string CategoryCode = "部門コード";
    public const string CategoryName = "部門";
    public const string Kind = "種別";
    public const string Price = "価格";
    public const string TaxIncluded = "内税";
    public const string TaxRateCode = "税率コード";
    public const string Cost = "原価";
    public const string PointRate = "還元率";
    public const string RequiresSerial = "シリアル要";
    public const string TrackInventory = "在庫管理";
    public const string AllowsPriceOverride = "売価変更可";
    public const string Unit = "単位";
    public const string IsActive = "販売可";
}
