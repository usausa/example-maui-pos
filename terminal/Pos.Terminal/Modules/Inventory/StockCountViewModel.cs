namespace Pos.Terminal.Modules.Inventory;

using Pos.Terminal.Modules.Dialogs;

public sealed record StockChangeItem(StockChange Change, string Name, string QuantityText, string Detail);

// 棚卸・在庫調整: スキャン → 現在庫 → 実数 (棚卸) or 増減 + 理由 (調整) → リスト → 送信 (StockUsecase)
public sealed partial class StockCountViewModel : AppViewModelBase
{
    private readonly IDialog dialog;

    private readonly IPopupNavigator popupNavigator;

    private readonly Session session;

    private StockContext stockContext = new();

    private readonly StockUsecase stock;

    public EntryController Code { get; }

    // 表題・案内文・送信ボタンの文言は画面側の Converter で切り替える
    [ObservableProperty]
    public partial bool IsAdjustment { get; set; }

    public ObservableCollection<StockChangeItem> Items { get; } = [];

    public IObserveCommand LookupCommand { get; }

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

        LookupCommand = MakeAsyncCommand(LookupAsync);
        Code = new EntryController(LookupCommand);
        RemoveCommand = MakeDelegateCommand<StockChangeItem>(x =>
        {
            stockContext.Changes.Remove(x.Change);
            Refresh();
        });
    }

    public override async Task OnNavigatedToAsync(INavigationContext context)
    {
        stockContext = context.Parameter.GetContext<StockContext>() ?? new StockContext();
        IsAdjustment = stockContext.IsAdjustment;
        Refresh();

        var scanned = context.Parameter.GetScanResult();
        if (scanned is not null)
        {
            await Navigator.PostActionAsync(() => HandleCodeAsync(scanned));
        }
    }

    private void Refresh()
    {
        Items.Replace(stockContext.Changes.Select(static x => new StockChangeItem(
            x,
            x.Product.Name,
            x.Type == InventoryChangeType.PhysicalCount ? $"実数 {DisplayText.Quantity(x.Quantity)}" : $"{(x.Quantity >= 0 ? "+" : string.Empty)}{DisplayText.Quantity(x.Quantity)}",
            $"{x.Product.Code}  現在庫 {DisplayText.Quantity(x.Before)}{(x.Reason is null ? string.Empty : "  " + x.Reason)}")));
    }

    private Task LookupAsync()
    {
        var code = Code.Text?.Trim();
        return String.IsNullOrEmpty(code) ? Task.CompletedTask : HandleCodeAsync(code);
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
        var existing = stockContext.Changes.FirstOrDefault(x => x.Product.Id == product.Id);
        if (IsAdjustment)
        {
            var text = await popupNavigator.InputNumberAsync($"増減数 (現在庫 {DisplayText.Quantity(before)})", "0", 6);
            if ((text is null) || !Decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out var delta) || (delta == 0))
            {
                return;
            }

            var direction = await dialog.ChooseAsync(["増やす (+)", "減らす (-)"], "増減");
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
                stockContext.Changes.Remove(existing);
            }

            stockContext.Changes.Add(new StockChange { Id = Guid.NewGuid(), Product = product, Type = InventoryChangeType.Adjustment, Quantity = delta, Before = before, ReasonId = reason.Id, Reason = reason.Text });
        }
        else
        {
            var text = await popupNavigator.InputNumberAsync($"実数 (現在庫 {DisplayText.Quantity(before)})", DisplayText.Quantity(before), 6);
            if ((text is null) || !Decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out var quantity))
            {
                return;
            }

            if (existing is not null)
            {
                stockContext.Changes.Remove(existing);
            }

            stockContext.Changes.Add(new StockChange { Id = Guid.NewGuid(), Product = product, Type = InventoryChangeType.PhysicalCount, Quantity = quantity, Before = before });
        }

        Code.Text = string.Empty;
        Refresh();
    }

    protected override async Task OnNotifyBackAsync()
    {
        if ((stockContext.Changes.Count > 0) && !await dialog.AskAsync("未送信の入力があります。破棄して戻りますか？", null, "破棄"))
        {
            return;
        }

        stockContext.Changes.Clear();
        await Navigator.ForwardAsync(ViewId.Menu);
    }

    protected override Task OnNotifyFunction1() => OnNotifyBackAsync();

    protected override Task OnNotifyFunction2() =>
        Navigator.ForwardAsync(ViewId.Scan, Parameters.Make().WithScan(ScanMode.ProductOnce, ViewId.StockCount).WithContext(stockContext));

    protected override Task OnNotifyFunction3()
    {
        stockContext.IsAdjustment = !stockContext.IsAdjustment;
        IsAdjustment = stockContext.IsAdjustment;
        return Task.CompletedTask;
    }

    protected override async Task OnNotifyFunction4()
    {
        if ((session.Store is null) || (session.Staff is null) || (stockContext.Changes.Count == 0))
        {
            return;
        }

        if (!await dialog.AskAsync($"{stockContext.Changes.Count} 件を送信しますか？", null, "送信"))
        {
            return;
        }

        await stock.SendAsync(stockContext.Changes.ToList());
        stockContext.Changes.Clear();
        await dialog.Toast("送信キューに入れました。");
        Refresh();
    }
}
