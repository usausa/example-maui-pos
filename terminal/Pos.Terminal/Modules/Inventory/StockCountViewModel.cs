namespace Pos.Terminal.Modules.Inventory;

using Pos.Terminal.Modules.Dialogs;

// 入力した 1 件 (種別の文言と帯の色は画面側の Converter で付ける)
public sealed record StockChangeItem(StockChange Change, string Name, string Code, string QuantityText, string Detail);

// 棚卸・在庫調整: スキャン → 現在庫 → 実数 (棚卸) or 増減 + 理由 (調整) → リスト → 送信 (StockUsecase)
public sealed partial class StockCountViewModel : AppViewModelBase
{
    private readonly IDialog dialog;

    private readonly IPopupNavigator popupNavigator;

    private readonly Session session;

    private readonly StockUsecase stock;

    // 棚卸の画面とスキャンで共有する状態 (Scope プラグインが注入する)
    [Scope]
    public StockContext StockContext { get; set; } = default!;

    // 表題・案内文・送信ボタンの文言は画面側の Converter で切り替える
    [ObservableProperty]
    public partial bool IsAdjustment { get; set; }

    public ObservableCollection<StockChangeItem> Items { get; } = [];

    public IObserveCommand InputCodeCommand { get; }

    public IObserveCommand RemoveCommand { get; }

    public StockCountViewModel(
        IDialog dialog,
        IPopupNavigator popupNavigator,
        Session session,
        StockUsecase stock)
    {
        this.dialog = dialog;
        this.popupNavigator = popupNavigator;
        this.session = session;
        this.stock = stock;

        InputCodeCommand = MakeAsyncCommand(InputCodeAsync);
        RemoveCommand = MakeDelegateCommand<StockChangeItem>(x =>
        {
            StockContext.Changes.Remove(x.Change);
            Refresh();
        });
    }

    public override async Task OnNavigatedToAsync(INavigationContext context)
    {
        IsAdjustment = StockContext.IsAdjustment;
        Refresh();

        var scanned = context.Parameter.GetScanResult();
        if (scanned is not null)
        {
            await Navigator.PostActionAsync(() => HandleCodeAsync(scanned));
        }
    }

    private void Refresh()
    {
        Items.Replace(StockContext.Changes.Select(static x => new StockChangeItem(
            x,
            x.Product.Name,
            x.Product.Code,
            x.Type == InventoryChangeType.PhysicalCount ? $"実数 {ViewHelper.Quantity(x.Quantity)}" : $"{(x.Quantity >= 0 ? "+" : string.Empty)}{ViewHelper.Quantity(x.Quantity)}",
            $"現在庫 {ViewHelper.Quantity(x.Before)}{(x.Reason is null ? string.Empty : "  " + x.Reason)}")));
    }

    // コードは電卓で入力する (キーボードに依存しない)
    private async Task InputCodeAsync()
    {
        var code = await popupNavigator.InputProductCodeAsync();
        if (!String.IsNullOrEmpty(code))
        {
            await HandleCodeAsync(code);
        }
    }

    private async Task HandleCodeAsync(string code)
    {
        if (session.StoreId is null)
        {
            return;
        }

        var product = await stock.FindProductAsync(code);
        if (product is null)
        {
            await dialog.InformationAsync($"商品が見つかりません: {code}");
            return;
        }

        if (!product.TrackInventory)
        {
            await dialog.InformationAsync($"{product.Name} は在庫管理対象外です。");
            return;
        }

        var before = await stock.QueryQuantityAsync(product.Id);

        // 同じ商品はリスト内で置き換える
        var existing = StockContext.Changes.FirstOrDefault(x => x.Product.Id == product.Id);
        if (IsAdjustment)
        {
            var text = await popupNavigator.InputStockAsync($"増減数 (現在庫 {ViewHelper.Quantity(before)})", 0m);
            if ((text is null) || !Decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out var delta) || (delta == 0))
            {
                return;
            }

            var direction = await popupNavigator.ChooseAsync(["増やす (+)", "減らす (-)"], "増減");
            if (direction < 0)
            {
                return;
            }

            if (direction == 1)
            {
                delta = -delta;
            }

            var reasons = (await stock.QueryReasonListAsync()).Where(static x => x.IsActive && !x.IsDeleted).OrderBy(static x => x.SortOrder)
                .Select(static x => new ReasonItem(x.Id, x.Name)).ToList();
            var reason = await popupNavigator.PopupAsync<ReasonSelectParameter, ReasonSelectResult?>(DialogId.ReasonSelect, new ReasonSelectParameter("調整理由", reasons, true));
            if (reason is null)
            {
                return;
            }

            if (existing is not null)
            {
                StockContext.Changes.Remove(existing);
            }

            StockContext.Changes.Add(new StockChange { Id = Guid.NewGuid(), Product = product, Type = InventoryChangeType.Adjustment, Quantity = delta, Before = before, ReasonId = reason.Id, Reason = reason.Text });
        }
        else
        {
            var text = await popupNavigator.InputStockAsync($"実数 (現在庫 {ViewHelper.Quantity(before)})", before);
            if ((text is null) || !Decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out var quantity))
            {
                return;
            }

            if (existing is not null)
            {
                StockContext.Changes.Remove(existing);
            }

            StockContext.Changes.Add(new StockChange { Id = Guid.NewGuid(), Product = product, Type = InventoryChangeType.PhysicalCount, Quantity = quantity, Before = before });
        }

        Refresh();
    }

    protected override async Task OnNotifyBackAsync()
    {
        if ((StockContext.Changes.Count > 0) && !await dialog.AskAsync("未送信の入力があります。破棄して戻りますか？", null, "破棄"))
        {
            return;
        }

        StockContext.Changes.Clear();
        await Navigator.ForwardAsync(ViewId.Menu);
    }

    protected override Task OnNotifyFunction1() => OnNotifyBackAsync();

    protected override Task OnNotifyFunction2() =>
        Navigator.ForwardAsync(ViewId.Scan, Parameters.Make().WithScan(ScanMode.ProductOnce, ViewId.StockCount));

    protected override Task OnNotifyFunction3()
    {
        StockContext.IsAdjustment = !StockContext.IsAdjustment;
        IsAdjustment = StockContext.IsAdjustment;
        return Task.CompletedTask;
    }

    protected override async Task OnNotifyFunction4()
    {
        if ((session.Store is null) || (session.Staff is null) || (StockContext.Changes.Count == 0))
        {
            return;
        }

        if (!await dialog.AskAsync($"{StockContext.Changes.Count} 件を送信しますか？", null, "送信"))
        {
            return;
        }

        await stock.SendAsync(StockContext.Changes.ToList());
        StockContext.Changes.Clear();
        await dialog.Toast("送信キューに入れました。");
        Refresh();
    }
}
