namespace Pos.Terminal.Modules.Inquiry;

using Pos.Contract.Customers;
using Pos.Contract.Transactions;
using Pos.Terminal.Models.Customer;

// 会員照会: 検索 / スキャンで会員を見つけ、基本情報・ポイント履歴・購入履歴 (オンライン) を見せる
public sealed partial class CustomerInquiryViewModel : AppViewModelBase
{
    private static readonly SummaryRow UnavailableRow = new("📡 取得できません", string.Empty);

    private readonly IDialog dialog;

    private readonly NetworkService network;

    private CustomerListResponseItem? customer;

    public EntryController Keyword { get; }

    public ObservableCollection<CustomerItem> Items { get; } = [];

    [ObservableProperty]
    public partial string Message { get; set; } = string.Empty;

    // 検索中 / 取得できない / 結果 (空文字) を切り替える
    [ObservableProperty]
    public partial string CurrentState { get; set; } = string.Empty;

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
        Keyword = new EntryController(EntryCompleteCommand.Create(SearchCommand));
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
        CurrentState = ViewHelper.LoadingState;

        var result = await network.ExecuteAsync(h => h.SearchCustomersAsync(keyword), notify: false);
        if (!result.IsSuccess)
        {
            Message = "取得できません";
            CurrentState = ViewHelper.OfflineState;
            return;
        }

        Items.Replace(result.Content!.Items
            .Select(static x => new CustomerItem(x, x.Name, x.Code, x.Phone ?? string.Empty, ViewHelper.Points(x.PointBalance))));
        Message = "該当する会員がいません";
        CurrentState = string.Empty;
    }

    private async Task UpdateCustomerAsync(CustomerListResponseItem value)
    {
        customer = value;
        HasCustomer = true;
        Name = value.Name + (value.Kana is null ? string.Empty : $" ({value.Kana})");
        Code = value.Code;
        PointsText = ViewHelper.Points(value.PointBalance);

        // 基本情報は先に出し (前の会員の履歴を残さない)、履歴 (オンライン) は取得してから足す
        Sections.Replace(
        [
            new SummarySection("ℹ️ 基本情報",
            [
                new SummaryRow("電話", value.Phone ?? "-"),
                new SummaryRow("メール", value.Email ?? "-"),
                new SummaryRow("住所", $"{value.PostalCode} {value.Address}".Trim()),
                new SummaryRow("生年月日", value.BirthDate is null ? "-" : ViewHelper.Date(value.BirthDate.Value)),
                new SummaryRow("備考", value.Note ?? "-"),
                new SummaryRow("登録", ViewHelper.Date(DateOnly.FromDateTime(value.CreatedAt.ToLocalTime())))
            ])
        ]);

        // 2 つの履歴は 1 回の実行で取り、通信中の表示を 1 回にする (続けて出し入れすると描画が止まることがある)
        var histories = await network.ExecuteAsync(async h =>
        {
            var points = await h.GetCustomerPointHistoryAsync(value.Id);
            if (!points.IsSuccess)
            {
                return Failed(points);
            }

            var transactions = await h.GetCustomerTransactionsAsync(value.Id);
            return transactions.IsSuccess
                ? new ApiResult<(CustomerPointHistoryResponse Points, TransactionListResponse Transactions)>(ApiStatus.Success, HttpStatusCode.OK, (points.Content!, transactions.Content!), null, null)
                : Failed(transactions);
        }, notify: false);

        // 取得の間に戻るや別の会員の選択で替わったら、前の会員の履歴は出さない
        if (customer != value)
        {
            return;
        }

        // 取得できない履歴も見出しを残し、取得できないことを示す
        Sections.Add(new SummarySection("🎁 ポイント履歴", histories.IsSuccess
            ? histories.Content.Points.Items
                .Select(static x => new SummaryRow($"{ViewHelper.DateTime(x.OccurredAt)}  {ViewHelper.Name(x.Type)}", $"{x.Points:+#,##0;-#,##0;0}  (残 {x.BalanceAfter:#,##0})"))
                .DefaultIfEmpty(new SummaryRow("履歴なし", string.Empty))
                .ToList()
            : [UnavailableRow]));
        Sections.Add(new SummarySection("🧾 購入履歴", histories.IsSuccess
            ? histories.Content.Transactions.Items
                .Select(static x => new SummaryRow($"{ViewHelper.DateTime(x.TransactedAt)}  {(x.Status == TransactionStatus.Voided ? ViewHelper.Name(x.Status) : ViewHelper.Name(x.Type))}\n{x.ReceiptNo}", ViewHelper.Yen(x.Total)))
                .DefaultIfEmpty(new SummaryRow("履歴なし", string.Empty))
                .ToList()
            : [UnavailableRow]));
    }

    private static ApiResult<(CustomerPointHistoryResponse Points, TransactionListResponse Transactions)> Failed<T>(ApiResult<T> result) =>
        new(result.Status, result.StatusCode, default, result.Problem, result.Exception);

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
