namespace Pos.Terminal.Modules.Sales;

using Pos.Terminal.Helpers.Data;

public sealed class CategoryItem : NotificationObject
{
    public Guid? Id { get; }

    public string Name { get; }

    public bool IsSelected
    {
        get;
        set => SetProperty(ref field, value);
    }

    public CategoryItem(Guid? id, string name)
    {
        Id = id;
        Name = name;
    }
}

// 検索結果の商品 (コードは等幅で見せ、シリアル必須はチップで示す。画像は結果を出した後に入れる)
public sealed class ProductItem : NotificationObject
{
    public ProductResponseItem Product { get; }

    public string Name { get; }

    public string Code { get; }

    public string Detail { get; }

    public string PriceText { get; }

    public bool RequiresSerial { get; }

    public ImageSource? Image
    {
        get;
        set => SetProperty(ref field, value);
    }

    public ProductItem(ProductResponseItem product, string detail, string priceText)
    {
        Product = product;
        Name = product.Name;
        Code = product.Code;
        Detail = detail;
        PriceText = priceText;
        RequiresSerial = product.RequiresSerial;
    }
}

// 商品検索: キーワードと部門 (2 階層) でローカルの商品を探す。販売からは追加して継続、照会からは選んで戻る
public sealed partial class ProductSearchViewModel : AppViewModelBase
{
    private readonly IDialog dialog;

    private readonly IPopupNavigator popupNavigator;

    private ViewId returnTo = ViewId.Sales;

    private readonly DataAccessor accessor;

    private readonly ProductImageService imageService;

    private readonly SalesUsecase sales;

    private IReadOnlyList<CategoryResponseItem> categories = [];

    private Dictionary<Guid, TaxRateResponseItem> taxRates = [];

    private CategoryItem? selectedParent;

    private CategoryItem? selectedChild;

    // 画像を入れている検索 (次の検索が始まったら前の結果には入れない)
    private int searchCount;

    // 販売の画面間で共有する状態 (Scope プラグインが同じインスタンスを注入し、どの画面からも参照されなくなると破棄する)。照会からのときは使わない
    [Scope]
    public SalesContext SalesContext { get; set; } = default!;

    public EntryController Keyword { get; }

    [ObservableProperty]
    public partial bool CategoryVisible { get; set; }

    public ObservableCollection<CategoryItem> Parents { get; } = [];

    public ObservableCollection<CategoryItem> Children { get; } = [];

    [ObservableProperty]
    public partial bool ChildrenVisible { get; set; }

    public ObservableCollection<ProductItem> Items { get; } = [];

    [ObservableProperty]
    public partial string Message { get; set; } = "キーワードか部門で検索してください。";

    public IObserveCommand SearchCommand { get; }

    public IObserveCommand InputNumberCommand { get; }

    public IObserveCommand SelectParentCommand { get; }

    public IObserveCommand SelectChildCommand { get; }

    public IObserveCommand SelectCommand { get; }

    public ProductSearchViewModel(
        IDialog dialog,
        IPopupNavigator popupNavigator,
        DataAccessor accessor,
        ProductImageService imageService,
        SalesUsecase sales)
    {
        this.dialog = dialog;
        this.popupNavigator = popupNavigator;
        this.accessor = accessor;
        this.imageService = imageService;
        this.sales = sales;

        SearchCommand = MakeAsyncCommand(SearchAsync);
        InputNumberCommand = MakeAsyncCommand(InputNumberAsync);
        Keyword = new EntryController(SearchCommand);
        SelectParentCommand = MakeAsyncCommand<CategoryItem>(SelectParentAsync);
        SelectChildCommand = MakeAsyncCommand<CategoryItem>(SelectChildAsync);
        SelectCommand = MakeAsyncCommand<ProductItem>(SelectAsync);
    }

    public override async Task OnNavigatedToAsync(INavigationContext context)
    {
        returnTo = context.Parameter.GetReturnTo(ViewId.Sales);
        await Navigator.PostActionAsync(LoadAsync);
    }

    private async Task LoadAsync()
    {
        categories = await accessor.QueryCategoryListAsync();
        taxRates = (await accessor.QueryTaxRateListAsync()).ToDictionary(static x => x.Id);
        Parents.Replace(new[] { new CategoryItem(null, "すべて") }.Concat(categories.Where(static x => x.ParentId is null).Select(static x => new CategoryItem(x.Id, x.Name))));
    }

    // 番号は電卓で入力する (キーボードに依存しない)
    private async Task InputNumberAsync()
    {
        var text = await popupNavigator.InputProductCodeAsync();
        if (!String.IsNullOrEmpty(text))
        {
            Keyword.Text = text;
            await SearchAsync();
        }
    }

    private async Task SearchAsync()
    {
        var keyword = Keyword.Text?.Trim();
        var pattern = SqlHelper.ToLikePattern(keyword);

        Guid[]? categoryIds = null;
        if (selectedChild?.Id is not null)
        {
            categoryIds = [selectedChild.Id.Value];
        }
        else if (selectedParent?.Id is not null)
        {
            categoryIds = categories.Where(x => x.ParentId == selectedParent.Id).Select(static x => x.Id).Append(selectedParent.Id.Value).ToArray();
        }

        var count = ++searchCount;
        if ((pattern is null) && (categoryIds is null))
        {
            Items.Clear();
            Message = "キーワードか部門で検索してください。";
            return;
        }

        var list = await accessor.QueryProductListAsync(categoryIds, pattern, 200);
        var items = list.Select(static x => new ProductItem(x, $"{x.ModelNo}  {x.Brand}".Trim(), ViewHelper.Yen(x.Price))).ToList();
        Items.Replace(items);
        Message = "該当する商品がありません。";

        // 画像は結果を出した後に上から順に入れる (取得を待たずに操作できる)
        foreach (var item in items.Where(static x => x.Product.ImageUrl is not null))
        {
            var file = await imageService.GetImageFileAsync(item.Product);
            if (count != searchCount)
            {
                return;
            }

            if (file is not null)
            {
                item.Image = ImageSource.FromFile(file);
            }
        }
    }

    private Task SelectParentAsync(CategoryItem item)
    {
        foreach (var parent in Parents)
        {
            parent.IsSelected = parent == item;
        }

        selectedParent = item;
        selectedChild = null;
        Children.Replace(item.Id is null ? [] : categories.Where(x => x.ParentId == item.Id).Select(static x => new CategoryItem(x.Id, x.Name)));
        ChildrenVisible = Children.Count > 0;
        return SearchAsync();
    }

    private Task SelectChildAsync(CategoryItem item)
    {
        var deselect = item.IsSelected;
        foreach (var child in Children)
        {
            child.IsSelected = (child == item) && !deselect;
        }

        selectedChild = deselect ? null : item;
        return SearchAsync();
    }

    private async Task SelectAsync(ProductItem item)
    {
        if (returnTo != ViewId.Sales)
        {
            await Navigator.ForwardAsync(returnTo, Parameters.Make().WithProductId(item.Product.Id));
            return;
        }

        if (!taxRates.TryGetValue(item.Product.TaxRateId, out var taxRate))
        {
            await dialog.InformationAsync("税率マスタがありません。同期してください。");
            return;
        }

        var line = SalesContext.Cart.Add(item.Product, taxRate);
        await dialog.Toast($"✓ {item.Product.Name} を追加 (×{ViewHelper.Quantity(line.Quantity)})  {ViewHelper.Yen(sales.Calculate(SalesContext.Cart, []).Total)}");
    }

    protected override Task OnNotifyBackAsync() => Navigator.ForwardAsync(returnTo);

    protected override Task OnNotifyFunction1() => OnNotifyBackAsync();

    protected override Task OnNotifyFunction2()
    {
        CategoryVisible = !CategoryVisible;
        return Task.CompletedTask;
    }

    protected override Task OnNotifyFunction3()
    {
        Keyword.Text = string.Empty;
        selectedParent = null;
        selectedChild = null;
        foreach (var parent in Parents)
        {
            parent.IsSelected = false;
        }

        Children.Clear();
        ChildrenVisible = false;
        return SearchAsync();
    }
}
