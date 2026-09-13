namespace Pos.Terminal.Modules.Inventory;

using Pos.Shared.Inventory;
using Pos.Terminal.Models.Entity;
using Pos.Terminal.Modules.Navigation.Modal;

using Smart.Data;

public sealed record StockChangeItem(StockChange Change, string Name, string QuantityText, string Detail);

// T-70 棚卸・在庫調整: スキャン → 現在庫 → 実数 (棚卸) or 増減 + 理由 (調整) → リスト → 送信 (Outbox)
public sealed partial class StockCountViewModel : AppViewModelBase
{
    private readonly IDialog dialog;

    private readonly IPopupNavigator popupNavigator;

    private readonly IDbProvider provider;

    private readonly DataAccessor accessor;

    private readonly Settings settings;

    private readonly Session session;

    private readonly StockState stock;

    private readonly SyncWorker syncWorker;

    public EntryController Code { get; }

    [ObservableProperty]
    public partial string Title { get; set; } = "棚卸";

    [ObservableProperty]
    public partial string ModeText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string SendText { get; set; } = "送信";

    [ObservableProperty]
    public partial IReadOnlyList<StockChangeItem> Items { get; set; } = [];

    [ObservableProperty]
    public partial bool HasChanges { get; set; }

    public IObserveCommand LookupCommand { get; }

    public IObserveCommand RemoveCommand { get; }

    public StockCountViewModel(
        IDialog dialog,
        IPopupNavigator popupNavigator,
        IDbProvider provider,
        DataAccessor accessor,
        Settings settings,
        Session session,
        StockState stock,
        SyncWorker syncWorker)
    {
        this.dialog = dialog;
        this.popupNavigator = popupNavigator;
        this.provider = provider;
        this.accessor = accessor;
        this.settings = settings;
        this.session = session;
        this.stock = stock;
        this.syncWorker = syncWorker;

        LookupCommand = MakeAsyncCommand(LookupAsync);
        Code = new EntryController(LookupCommand);
        RemoveCommand = MakeDelegateCommand<StockChangeItem>(x =>
        {
            stock.Changes.Remove(x.Change);
            Refresh();
        });
    }

    public override Task OnNavigatedToAsync(INavigationContext context)
    {
        UpdateMode();
        Refresh();

        var scanned = context.Parameter.GetScanResult();
        return scanned is null ? Task.CompletedTask : HandleCodeAsync(scanned);
    }

    private void UpdateMode()
    {
        Title = stock.IsAdjustment ? "在庫調整" : "棚卸";
        ModeText = stock.IsAdjustment
            ? "🔧 調整モード: 増減数と理由を入力します。F3 で棚卸に切替"
            : "📋 棚卸モード: 実際の在庫数を入力します。F3 で調整に切替";
    }

    private void Refresh()
    {
        Items = stock.Changes.Select(static x => new StockChangeItem(
            x,
            x.Product.Name,
            x.Type == InventoryChangeType.PhysicalCount ? $"実数 {DisplayText.Quantity(x.Quantity)}" : $"{(x.Quantity >= 0 ? "+" : string.Empty)}{DisplayText.Quantity(x.Quantity)}",
            $"{x.Product.Code}  現在庫 {DisplayText.Quantity(x.Before)}{(x.Reason is null ? string.Empty : "  " + x.Reason)}")).ToList();
        HasChanges = Items.Count > 0;
        SendText = HasChanges ? $"送信 ({Items.Count})" : "送信";
    }

    private Task LookupAsync()
    {
        var code = Code.Text?.Trim();
        return String.IsNullOrEmpty(code) ? Task.CompletedTask : HandleCodeAsync(code);
    }

    private async Task HandleCodeAsync(string code)
    {
        if (settings.StoreId is null)
        {
            return;
        }

        var product = await accessor.QueryProductByBarcodeAsync(code) ?? await accessor.QueryProductByCodeAsync(code);
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

        var level = await accessor.QueryInventoryLevelAsync(settings.StoreId.Value, product.Id);
        var before = level?.Quantity ?? 0m;

        // 同じ商品はリスト内で置き換える
        var existing = stock.Changes.FirstOrDefault(x => x.Product.Id == product.Id);

        if (stock.IsAdjustment)
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

            var reasons = (await accessor.QueryAdjustmentReasonListAsync()).Where(static x => x.IsActive && !x.IsDeleted).OrderBy(static x => x.SortOrder)
                .Select(static x => new ReasonItem(x.Id, x.Name)).ToList();
            var reason = await popupNavigator.PopupAsync<ReasonSelectParameter, ReasonSelectResult?>(DialogId.ReasonSelect, new ReasonSelectParameter("調整理由", reasons, true));
            if (reason is null)
            {
                return;
            }

            if (existing is not null)
            {
                stock.Changes.Remove(existing);
            }

            stock.Changes.Add(new StockChange { Id = Guid.NewGuid(), Product = product, Type = InventoryChangeType.Adjustment, Quantity = delta, Before = before, ReasonId = reason.Id, Reason = reason.Text });
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
                stock.Changes.Remove(existing);
            }

            stock.Changes.Add(new StockChange { Id = Guid.NewGuid(), Product = product, Type = InventoryChangeType.PhysicalCount, Quantity = quantity, Before = before });
        }

        Code.Text = string.Empty;
        Refresh();
    }

    protected override async Task OnNotifyBackAsync()
    {
        if (HasChanges && !await dialog.AskAsync("未送信の入力があります。破棄して戻りますか？", null, "破棄"))
        {
            return;
        }

        stock.Changes.Clear();
        await Navigator.ForwardAsync(ViewId.Menu);
    }

    protected override Task OnNotifyFunction1() => OnNotifyBackAsync();

    protected override Task OnNotifyFunction2() =>
        Navigator.ForwardAsync(ViewId.Scan, Parameters.Make().WithScan(ScanMode.ProductOnce, ViewId.StockCount));

    protected override Task OnNotifyFunction3()
    {
        stock.IsAdjustment = !stock.IsAdjustment;
        UpdateMode();
        return Task.CompletedTask;
    }

    protected override async Task OnNotifyFunction4()
    {
        if ((settings.StoreId is null) || (session.Staff is null) || (stock.Changes.Count == 0))
        {
            return;
        }

        if (!await dialog.AskAsync($"{stock.Changes.Count} 件を送信しますか？", null, "送信"))
        {
            return;
        }

        var now = DateTime.UtcNow;
        var storeId = settings.StoreId.Value;
        var request = new InventoryChangeRequest
        {
            Changes = stock.Changes.Select(x => new InventoryChangeRequestChange
            {
                Id = x.Id,
                StoreId = storeId,
                ProductId = x.Product.Id,
                Type = x.Type,
                Quantity = x.Quantity,
                ReasonId = x.ReasonId,
                Reason = x.Reason,
                StaffId = session.Staff.Id,
                OccurredAt = now
            }).ToList()
        };

        // ローカルの在庫キャッシュも同じように動かす
        await provider.UsingTxAsync(async (_, tx) =>
        {
            foreach (var change in stock.Changes)
            {
                if (change.Type == InventoryChangeType.PhysicalCount)
                {
                    await accessor.SetInventoryQuantityAsync(tx, storeId, change.Product.Id, change.Quantity, now);
                }
                else
                {
                    await accessor.AddInventoryQuantityAsync(tx, storeId, change.Product.Id, change.Quantity, now);
                }
            }

            await accessor.InsertOutboxAsync(tx, SyncWorker.CreateEntry(OutboxKind.InventoryChanges, Guid.NewGuid(), request, now));
            await tx.CommitAsync();
        });

        stock.Changes.Clear();
        await syncWorker.UpdateCountsAsync();
        syncWorker.Trigger();

        await dialog.Toast("送信キューに入れました。");
        Refresh();
    }
}
