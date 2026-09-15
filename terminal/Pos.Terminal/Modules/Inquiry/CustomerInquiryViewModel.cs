namespace Pos.Terminal.Modules.Inquiry;

using Pos.Contract.Customers;
using Pos.Terminal.Modules.Sales;

// 会員照会: 検索 / スキャンで会員を見つけ、基本情報・ポイント履歴・購入履歴 (オンライン) を見せる
public sealed partial class CustomerInquiryViewModel : AppViewModelBase
{
    private readonly IDialog dialog;

    private readonly NetworkService network;

    private CustomerResponseItem? customer;

    public EntryController Keyword { get; }

    public ObservableCollection<CustomerItem> Items { get; } = [];

    [ObservableProperty]
    public partial string Message { get; set; } = "会員番号・電話番号・名前で検索するか、会員証をスキャンしてください。";

    [ObservableProperty]
    public partial bool HasCustomer { get; set; }

    [ObservableProperty]
    public partial string Name { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Code { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string PointsText { get; set; } = string.Empty;

    public ObservableCollection<SummarySection> Sections { get; } = [];

    public IObserveCommand SearchCommand { get; }

    public IObserveCommand SelectCommand { get; }

    public CustomerInquiryViewModel(
        IDialog dialog,
        NetworkService network)
    {
        this.dialog = dialog;
        this.network = network;

        SearchCommand = MakeAsyncCommand(SearchAsync);
        Keyword = new EntryController(SearchCommand);
        SelectCommand = MakeAsyncCommand<CustomerItem>(x => UpdateCustomerAsync(x.Customer));
    }

    public override async Task OnNavigatedToAsync(INavigationContext context)
    {
        // 編集からの戻り
        var edited = context.Parameter.GetCustomer();
        if (edited is not null)
        {
            await Navigator.PostActionAsync(() => UpdateCustomerAsync(edited));
            return;
        }

        var id = context.Parameter.GetCustomerId();
        if (id is not null)
        {
            await Navigator.PostActionAsync(() => LoadAsync(id.Value));
            return;
        }

        var scanned = context.Parameter.GetScanResult();
        if (scanned is not null)
        {
            await Navigator.PostActionAsync(() => LookupAsync(scanned));
            return;
        }

        Keyword.Focus();
    }

    private async Task LoadAsync(Guid id)
    {
        var result = await network.ExecuteAsync(h => h.GetCustomerAsync(id), notifyNotFound: true);
        if (result is { IsSuccess: true, Content: not null })
        {
            await UpdateCustomerAsync(result.Content);
        }
    }

    private async Task LookupAsync(string code)
    {
        var result = await network.ExecuteAsync(h => h.LookupCustomerAsync(code));
        if (result is { IsSuccess: true, Content: not null })
        {
            await UpdateCustomerAsync(result.Content);
            return;
        }

        if (result.IsNotFound)
        {
            await dialog.InformationAsync($"会員が見つかりません: {code}");
        }
    }

    private async Task SearchAsync()
    {
        var keyword = Keyword.Text?.Trim();
        if (String.IsNullOrEmpty(keyword))
        {
            return;
        }

        HasCustomer = false;
        customer = null;

        var result = await network.ExecuteAsync(h => h.SearchCustomersAsync(keyword));
        if (!result.IsSuccess)
        {
            return;
        }

        Items.Replace(result.Content!.Items
            .Select(static x => new CustomerItem(x, x.Name, ViewHelper.Points(x.PointBalance), $"{x.Code}  {x.Phone}".Trim())));
        Message = "該当する会員がいません。";
    }

    private async Task UpdateCustomerAsync(CustomerResponseItem value)
    {
        customer = value;
        HasCustomer = true;
        Name = value.Name + (value.Kana is null ? string.Empty : $" ({value.Kana})");
        Code = value.Code;
        PointsText = ViewHelper.Points(value.PointBalance);

        var sections = new List<SummarySection>
        {
            new("ℹ 基本情報",
            [
                new SummaryRow("電話", value.Phone ?? "-"),
                new SummaryRow("メール", value.Email ?? "-"),
                new SummaryRow("住所", $"{value.PostalCode} {value.Address}".Trim()),
                new SummaryRow("生年月日", value.BirthDate is null ? "-" : ViewHelper.Date(value.BirthDate.Value)),
                new SummaryRow("備考", value.Note ?? "-"),
                new SummaryRow("登録", ViewHelper.Date(DateOnly.FromDateTime(value.CreatedAt.ToLocalTime())))
            ])
        };

        var points = await network.ExecuteAsync(h => h.GetCustomerPointHistoryAsync(value.Id), notify: false);
        if (points.IsSuccess)
        {
            sections.Add(new SummarySection("🎁 ポイント履歴", points.Content!.Items
                .Select(static x => new SummaryRow($"{ViewHelper.DateTime(x.OccurredAt)}  {ViewHelper.Name(x.Type)}", $"{x.Points:+#,##0;-#,##0;0}  (残 {x.BalanceAfter:#,##0})"))
                .DefaultIfEmpty(new SummaryRow("履歴なし", string.Empty))
                .ToList()));
        }

        var transactions = await network.ExecuteAsync(h => h.GetCustomerTransactionsAsync(value.Id), notify: false);
        if (transactions.IsSuccess)
        {
            sections.Add(new SummarySection("🧾 購入履歴", transactions.Content!.Items
                .Select(static x => new SummaryRow($"{ViewHelper.DateTime(x.TransactedAt)}  {(x.Status == TransactionStatus.Voided ? "取消" : ViewHelper.Name(x.Type))}\n{x.ReceiptNo}", ViewHelper.Yen(x.Total)))
                .DefaultIfEmpty(new SummaryRow("履歴なし", string.Empty))
                .ToList()));
        }

        Sections.Replace(sections);
    }

    protected override Task OnNotifyBackAsync()
    {
        if (HasCustomer && (Items.Count > 0))
        {
            HasCustomer = false;
            customer = null;
            return Task.CompletedTask;
        }

        return Navigator.ForwardAsync(ViewId.Menu);
    }

    protected override Task OnNotifyFunction1() => OnNotifyBackAsync();

    protected override Task OnNotifyFunction2() =>
        Navigator.ForwardAsync(ViewId.Scan, Parameters.Make().WithScan(ScanMode.Customer, ViewId.CustomerInquiry));

    protected override Task OnNotifyFunction3()
    {
        HasCustomer = false;
        customer = null;
        Keyword.Focus();
        return Task.CompletedTask;
    }

    protected override Task OnNotifyFunction4() =>
        customer is null
            ? Task.CompletedTask
            : Navigator.ForwardAsync(ViewId.CustomerEdit, Parameters.Make().WithReturnTo(ViewId.CustomerInquiry).WithCustomer(customer));
}
