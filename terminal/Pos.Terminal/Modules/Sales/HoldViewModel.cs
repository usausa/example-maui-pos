namespace Pos.Terminal.Modules.Sales;

using System.Text.Json;

using Pos.Terminal.Models.Entity;
using Pos.Terminal.Models.Sales;

public sealed class HoldItem : NotificationObject
{
    public HoldCartEntity Entity { get; }

    public string Summary => Entity.Summary;

    public string TotalText => DisplayText.Yen(Entity.Total);

    public string TimeText => "⏸ " + DisplayText.DateTime(Entity.CreatedAt);

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

// T-17 保留・呼出: 端末ローカルの保留一覧から呼び出す / 破棄する
public sealed partial class HoldViewModel : AppViewModelBase
{
    private readonly IDialog dialog;

    private readonly DataAccessor accessor;

    private readonly SalesState sales;

    private HoldItem? selected;

    [ObservableProperty]
    public partial IReadOnlyList<HoldItem> Items { get; set; } = [];

    [ObservableProperty]
    public partial bool HasSelection { get; set; }

    public IObserveCommand SelectCommand { get; }

    public HoldViewModel(
        IDialog dialog,
        DataAccessor accessor,
        SalesState sales)
    {
        this.dialog = dialog;
        this.accessor = accessor;
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
        await LoadAsync();
    }

    private async ValueTask LoadAsync()
    {
        Items = (await accessor.QueryHoldCartListAsync()).Select(static x => new HoldItem(x)).ToList();
        selected = null;
        HasSelection = false;
    }

    protected override Task OnNotifyBackAsync() => Navigator.ForwardAsync(ViewId.Sales);

    protected override Task OnNotifyFunction1() => OnNotifyBackAsync();

    protected override async Task OnNotifyFunction3()
    {
        if ((selected is null) || !await dialog.AskAsync($"{selected.Summary} を破棄しますか？", null, "破棄"))
        {
            return;
        }

        await accessor.DeleteHoldCartAsync(selected.Entity.Id);
        await LoadAsync();
    }

    protected override async Task OnNotifyFunction4()
    {
        if (selected is null)
        {
            return;
        }

        if (!sales.Cart.IsEmpty && !await dialog.AskAsync("現在の明細を破棄して呼び出しますか？", null, "呼出"))
        {
            return;
        }

        var cart = JsonSerializer.Deserialize<Cart>(selected.Entity.Payload, HttpService.JsonOptions);
        if (cart is null)
        {
            await dialog.InformationAsync("保留データを読めませんでした。");
            return;
        }

        sales.ResetSale();
        sales.Cart = cart;
        await accessor.DeleteHoldCartAsync(selected.Entity.Id);
        await Navigator.ForwardAsync(ViewId.Sales);
    }
}
