namespace Pos.Terminal.Modules.Inquiry;

// 商品・在庫照会: スキャン / 検索で価格・税・還元率・自店在庫を見せる。他店在庫はオンライン
public sealed partial class ProductInquiryViewModel : AppViewModelBase
{
    private readonly Session session;

    private readonly DataAccessor accessor;

    private readonly NetworkService network;

    private ProductResponseItem? product;

    [ObservableProperty]
    public partial string Message { get; set; } = "商品をスキャンするか、検索してください。";

    [ObservableProperty]
    public partial bool HasProduct { get; set; }

    [ObservableProperty]
    public partial string Name { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Code { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string PriceText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string PriceDetail { get; set; } = string.Empty;

    public ObservableCollection<SummarySection> Sections { get; } = [];

    // 他店在庫 (F4 で取得したときだけ見出しと面を出す。取得中・取得できない・在庫なしは案内文で示す)
    [ObservableProperty]
    public partial bool HasOtherStores { get; set; }

    [ObservableProperty]
    public partial string OtherStoresMessage { get; set; } = string.Empty;

    public ObservableCollection<SummaryRow> OtherStores { get; } = [];

    public ProductInquiryViewModel(
        Session session,
        DataAccessor accessor,
        NetworkService network)
    {
        this.session = session;
        this.accessor = accessor;
        this.network = network;
    }

    public override async Task OnNavigatedToAsync(INavigationContext context)
    {
        var id = context.Parameter.GetProductId();
        var scanned = context.Parameter.GetScanResult();
        if (id is not null)
        {
            await Navigator.PostActionAsync(async () => await UpdateProductAsync(await accessor.QueryProductAsync(id.Value), id.Value.ToString()));
        }
        else if (scanned is not null)
        {
            await Navigator.PostActionAsync(async () => await UpdateProductAsync(await accessor.QueryProductByBarcodeAsync(scanned) ?? await accessor.QueryProductByCodeAsync(scanned), scanned));
        }
    }

    private async Task UpdateProductAsync(ProductResponseItem? value, string key)
    {
        product = value;
        HasOtherStores = false;
        OtherStores.Clear();
        if (value is null)
        {
            HasProduct = false;
            Message = $"❌ 商品が見つかりません: {key}";
            return;
        }

        HasProduct = true;
        Name = value.Name;
        Code = $"{value.Code}  {value.Barcode}".Trim();
        PriceText = ViewHelper.Yen(value.Price);

        var taxRate = (await accessor.QueryTaxRateListAsync()).FirstOrDefault(x => x.Id == value.TaxRateId);
        var category = (await accessor.QueryCategoryListAsync()).FirstOrDefault(x => x.Id == value.CategoryId);
        PriceDetail = $"{(value.TaxIncluded ? "税込" : "税抜")} {(taxRate is null ? string.Empty : ViewHelper.Percent(taxRate.Rate))}  還元率 {ViewHelper.Percent(value.PointRate)}";

        var level = session.StoreId is null ? null : await accessor.QueryInventoryLevelAsync(session.StoreId.Value, value.Id);
        Sections.Replace(
        [
            new SummarySection("📦 自店在庫",
            [
                new SummaryRow(value.TrackInventory ? "在庫数" : "在庫管理対象外", value.TrackInventory ? ViewHelper.Quantity(level?.Quantity ?? 0m) + (value.Unit ?? string.Empty) : "-"),
                new SummaryRow("更新", level is null ? "-" : ViewHelper.DateTime(level.UpdatedAt))
            ]),
            new SummarySection("ℹ 商品情報",
            [
                new SummaryRow("部門", category?.Name ?? "-"),
                new SummaryRow("ブランド / 型番", $"{value.Brand} {value.ModelNo}".Trim()),
                new SummaryRow("種別", value.Kind == ProductKind.Service ? "サービス" : "商品"),
                new SummaryRow("シリアル", value.RequiresSerial ? "必須" : "不要"),
                new SummaryRow("状態", value.IsActive && !value.IsDeleted ? "販売中" : "取扱終了")
            ])
        ]);
    }

    protected override Task OnNotifyBackAsync() => Navigator.ForwardAsync(ViewId.Menu);

    protected override Task OnNotifyFunction1() => OnNotifyBackAsync();

    protected override Task OnNotifyFunction2() =>
        Navigator.ForwardAsync(ViewId.Scan, Parameters.Make().WithScan(ScanMode.ProductOnce, ViewId.ProductInquiry));

    protected override Task OnNotifyFunction3() =>
        Navigator.ForwardAsync(ViewId.ProductSearch, Parameters.Make().WithReturnTo(ViewId.ProductInquiry));

    // 他店在庫 (オンライン限定)
    protected override async Task OnNotifyFunction4()
    {
        if (product is null)
        {
            return;
        }

        HasOtherStores = true;
        OtherStores.Clear();
        OtherStoresMessage = "取得しています。";
        var result = await network.ExecuteAsync(h => h.GetProductInventoryAsync(product.Id), notifyNotFound: true);
        if (!result.IsSuccess)
        {
            OtherStoresMessage = "📡 取得できませんでした。オンラインで「他店在庫」を押してください。";
            return;
        }

        OtherStores.Replace(result.Content!.Levels
            .Select(x => new SummaryRow((x.StoreId == session.StoreId ? "🏪 " : string.Empty) + x.StoreName, ViewHelper.Quantity(x.Quantity))));
        OtherStoresMessage = OtherStores.Count == 0 ? "在庫のある店舗はありません。" : string.Empty;
    }
}
