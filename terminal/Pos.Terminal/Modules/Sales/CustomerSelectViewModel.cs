namespace Pos.Terminal.Modules.Sales;

using Pos.Shared.Customers;

public sealed record CustomerItem(CustomerResponse Customer, string Name, string PointsText, string Detail);

// T-14 会員選択: 検索 (オンライン) またはスキャンで取引に会員を紐付ける
public sealed partial class CustomerSelectViewModel : AppViewModelBase
{
    private readonly IDialog dialog;

    private readonly NetworkOperator network;

    private readonly SalesState sales;

    private ViewId returnTo = ViewId.Sales;

    public EntryController Keyword { get; }

    [ObservableProperty]
    public partial bool HasCustomer { get; set; }

    [ObservableProperty]
    public partial string CurrentText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial IReadOnlyList<CustomerItem> Items { get; set; } = [];

    [ObservableProperty]
    public partial string EmptyText { get; set; } = "会員番号・電話番号・名前で検索するか、会員証をスキャンしてください。";

    public IObserveCommand SearchCommand { get; }

    public IObserveCommand SelectCommand { get; }

    public CustomerSelectViewModel(
        IDialog dialog,
        NetworkOperator network,
        SalesState sales)
    {
        this.dialog = dialog;
        this.network = network;
        this.sales = sales;

        SearchCommand = MakeAsyncCommand(SearchAsync);
        Keyword = new EntryController(SearchCommand);
        SelectCommand = MakeAsyncCommand<CustomerItem>(x => ApplyAsync(x.Customer));
    }

    public override async Task OnNavigatedToAsync(INavigationContext context)
    {
        returnTo = context.Parameter.GetCallerReturnTo() ?? context.Parameter.GetReturnTo(ViewId.Sales);
        var current = sales.Cart.Customer;
        HasCustomer = current is not null;
        CurrentText = current is null ? string.Empty : $"👤 現在: {current.Name} ({current.Code})";

        // スキャン結果 (会員証)
        var scanned = context.Parameter.GetScanResult();
        if (scanned is not null)
        {
            var result = await network.ExecuteAsync(h => h.LookupCustomerAsync(scanned));
            if (result is { IsSuccess: true, Content: not null })
            {
                await ApplyAsync(result.Content);
                return;
            }

            if (result.IsNotFound)
            {
                await dialog.InformationAsync($"会員が見つかりません: {scanned}");
            }
        }

        Keyword.Focus();
    }

    private async Task SearchAsync()
    {
        var keyword = Keyword.Text?.Trim();
        if (String.IsNullOrEmpty(keyword))
        {
            return;
        }

        var result = await network.ExecuteAsync(h => h.SearchCustomersAsync(keyword));
        if (!result.IsSuccess)
        {
            return;
        }

        Items = result.Content!.Items
            .Where(static x => !x.IsDeleted)
            .Select(static x => new CustomerItem(x, x.Name, DisplayText.Points(x.PointBalance), $"{x.Code}  {x.Phone}".Trim()))
            .ToList();
        EmptyText = "該当する会員がいません。";
    }

    private async Task ApplyAsync(CustomerResponse customer)
    {
        sales.Cart.Customer = customer;
        await dialog.Toast($"👤 {customer.Name} ({DisplayText.Points(customer.PointBalance)})");
        await Navigator.ForwardAsync(returnTo);
    }

    protected override Task OnNotifyBackAsync() => Navigator.ForwardAsync(returnTo);

    protected override Task OnNotifyFunction1() => OnNotifyBackAsync();

    protected override Task OnNotifyFunction2() =>
        Navigator.ForwardAsync(ViewId.Scan, Parameters.Make().WithScan(ScanMode.Customer, ViewId.CustomerSelect, returnTo));

    protected override Task OnNotifyFunction3() =>
        Navigator.ForwardAsync(ViewId.CustomerEdit, Parameters.Make().WithReturnTo(returnTo).WithApplyToCart());

    protected override async Task OnNotifyFunction4()
    {
        sales.Cart.Customer = null;
        await Navigator.ForwardAsync(returnTo);
    }
}
