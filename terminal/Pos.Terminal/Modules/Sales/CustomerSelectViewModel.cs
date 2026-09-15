namespace Pos.Terminal.Modules.Sales;

using Pos.Contract.Customers;

public sealed record CustomerItem(CustomerResponseItem Customer, string Name, string PointsText, string Detail);

// 会員選択: 検索 (オンライン) またはスキャンで取引に会員を紐付ける
public sealed partial class CustomerSelectViewModel : AppViewModelBase
{
    private readonly IDialog dialog;

    private readonly IPopupNavigator popupNavigator;

    private ViewId returnTo = ViewId.Sales;

    private readonly NetworkService network;

    // 販売の画面間で共有する状態 (Scope プラグインが同じインスタンスを注入し、どの画面からも参照されなくなると破棄する)
    [Scope]
    public SalesContext SalesContext { get; set; } = default!;

    public EntryController Keyword { get; }

    [ObservableProperty]
    public partial bool HasCustomer { get; set; }

    [ObservableProperty]
    public partial string CurrentText { get; set; } = string.Empty;

    public ObservableCollection<CustomerItem> Items { get; } = [];

    [ObservableProperty]
    public partial string Message { get; set; } = "会員番号・電話番号・名前で検索するか、会員証をスキャンしてください。";

    public IObserveCommand SearchCommand { get; }

    public IObserveCommand InputNumberCommand { get; }

    public IObserveCommand SelectCommand { get; }

    public CustomerSelectViewModel(
        IDialog dialog,
        IPopupNavigator popupNavigator,
        NetworkService network)
    {
        this.dialog = dialog;
        this.popupNavigator = popupNavigator;
        this.network = network;

        SearchCommand = MakeAsyncCommand(SearchAsync);
        InputNumberCommand = MakeAsyncCommand(InputNumberAsync);
        Keyword = new EntryController(SearchCommand);
        SelectCommand = MakeAsyncCommand<CustomerItem>(x => ApplyAsync(x.Customer));
    }

    public override async Task OnNavigatedToAsync(INavigationContext context)
    {
        returnTo = context.Parameter.GetCallerReturnTo() ?? context.Parameter.GetReturnTo(ViewId.Sales);
        var current = SalesContext.Cart.Customer;
        HasCustomer = current is not null;
        CurrentText = current is null ? string.Empty : $"👤 現在: {current.Name} ({current.Code})";

        // スキャン結果 (会員証)
        var scanned = context.Parameter.GetScanResult();
        if (scanned is not null)
        {
            await Navigator.PostActionAsync(() => LookupAsync(scanned));
        }
    }

    private async Task LookupAsync(string code)
    {
        var result = await network.ExecuteAsync(h => h.LookupCustomerAsync(code));
        if (result is { IsSuccess: true, Content: not null })
        {
            await ApplyAsync(result.Content);
            return;
        }

        if (result.IsNotFound)
        {
            await dialog.InformationAsync($"会員が見つかりません: {code}");
        }
    }

    // 番号は電卓で入力する (キーボードに依存しない)
    private async Task InputNumberAsync()
    {
        var text = await popupNavigator.InputCustomerNoAsync();
        if (!String.IsNullOrEmpty(text))
        {
            Keyword.Text = text;
            await SearchAsync();
        }
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

        Items.Replace(result.Content!.Items
            .Where(static x => !x.IsDeleted)
            .Select(static x => new CustomerItem(x, x.Name, ViewHelper.Points(x.PointBalance), $"{x.Code}  {x.Phone}".Trim())));
        Message = "該当する会員がいません。";
    }

    private async Task ApplyAsync(CustomerResponseItem customer)
    {
        SalesContext.Cart.Customer = customer;
        await dialog.Toast($"👤 {customer.Name} ({ViewHelper.Points(customer.PointBalance)})");
        await Navigator.ForwardAsync(returnTo);
    }

    protected override Task OnNotifyBackAsync() => Navigator.ForwardAsync(returnTo);

    protected override Task OnNotifyFunction1() => OnNotifyBackAsync();

    protected override Task OnNotifyFunction2() =>
        Navigator.ForwardAsync(ViewId.Scan, Parameters.Make().WithScan(ScanMode.Customer, ViewId.CustomerSelect, returnTo));

    // 新規登録 (登録後はカートへ紐付けて呼び出し元へ)
    protected override Task OnNotifyFunction3() =>
        Navigator.ForwardAsync(ViewId.CustomerEdit, Parameters.Make().WithReturnTo(returnTo));

    protected override async Task OnNotifyFunction4()
    {
        SalesContext.Cart.Customer = null;
        await Navigator.ForwardAsync(returnTo);
    }
}
