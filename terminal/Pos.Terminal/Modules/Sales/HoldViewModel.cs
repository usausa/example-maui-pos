namespace Pos.Terminal.Modules.Sales;

using Pos.Terminal.Models.Entity;

public sealed class HoldItem : NotificationObject
{
    public HoldCartEntity Entity { get; }

    public string Summary => Entity.Summary;

    public string TotalText => ViewHelper.Yen(Entity.Total);

    public string TimeText => "⏸️ " + ViewHelper.DateTime(Entity.CreatedAt);

    public bool IsSelected
    {
        get;
        set => SetProperty(ref field, value);
    }

    public HoldItem(HoldCartEntity entity)
    {
        Entity = entity;
    }
}

// 保留・呼出: 端末ローカルの保留一覧から呼び出す / 破棄する
public sealed partial class HoldViewModel : AppViewModelBase
{
    private readonly IDialog dialog;

    private readonly SalesUsecase sales;

    private HoldItem? selected;

    // 販売の画面間で共有する状態 (Scope プラグインが同じインスタンスを注入し、どの画面からも参照されなくなると破棄する)
    [Scope]
    public SalesContext SalesContext { get; set; } = default!;

    public ObservableCollection<HoldItem> Items { get; } = [];

    [ObservableProperty]
    public partial bool HasSelection { get; set; }

    public IObserveCommand SelectCommand { get; }

    public HoldViewModel(
        IDialog dialog,
        SalesUsecase sales)
    {
        this.dialog = dialog;
        this.sales = sales;

        SelectCommand = MakeDelegateCommand<HoldItem>(x =>
        {
            foreach (var item in Items)
            {
                item.IsSelected = item == x;
            }

            selected = x;
            HasSelection = true;
        });
    }

    public override async Task OnNavigatedToAsync(INavigationContext context)
    {
        await Navigator.PostActionAsync(LoadAsync);
    }

    private async Task LoadAsync()
    {
        Items.Replace((await sales.QueryHoldListAsync()).Select(static x => new HoldItem(x)));
        selected = null;
        HasSelection = false;
    }

    private Task<bool> ReturnAsync() => Navigator.ForwardAsync(ViewId.Sales);

    protected override Task OnNotifyBackAsync() => ReturnAsync();

    protected override Task OnNotifyFunction1() => OnNotifyBackAsync();

    protected override async Task OnNotifyFunction3()
    {
        if ((selected is null) || !await dialog.AskAsync($"{selected.Summary} を破棄しますか？", null, "破棄"))
        {
            return;
        }

        await sales.DiscardHoldAsync(selected.Entity.Id);
        await LoadAsync();
    }

    protected override async Task OnNotifyFunction4()
    {
        if (selected is null)
        {
            return;
        }

        if (!SalesContext.Cart.IsEmpty && !await dialog.AskAsync("現在の明細を破棄して呼び出しますか？", null, "呼出"))
        {
            return;
        }

        var cart = await sales.RecallAsync(selected.Entity);
        if (cart is null)
        {
            await dialog.InformationAsync("保留データを読めませんでした。");
            return;
        }

        SalesContext.Reset();
        SalesContext.Cart = cart;
        await ReturnAsync();
    }
}
