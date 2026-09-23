namespace Pos.Terminal.Modules.Report;

using Pos.Contract.Reports;

// 合計のタイル (件数・金額は表示用の文字列)
public sealed record ReportTotals(
    string SalesCount,
    string SalesTotal,
    string ReturnCount,
    string ReturnsTotal,
    string Discount,
    string Tax,
    string CustomerCount,
    string PointsEarned,
    string PointsRedeemed)
{
    public static ReportTotals Empty { get; } = new("-", "-", "-", "-", "-", "-", "-", "-", "-");
}

// 集計軸ごとの行。Ratio は純売上に占める割合 (0〜1)
public sealed record ReportGroupRow(string Label, string CountText, string AmountText, double Ratio);

// 売上照会: サーバの売上集計 (オンライン限定)。期間・範囲 (自端末 / 自店 / 全店)・集計軸を切り替える
public sealed partial class SalesReportViewModel : AppViewModelBase
{
    private static readonly string[] Periods = ["本日", "昨日", "今週", "今月"];

    private static readonly string[] Scopes = ["自端末", "自店", "全店"];

    private static readonly (string Name, string GroupBy)[] Groups =
    [
        ("支払方法別", "paymentMethod"),
        ("時間帯別", "hour"),
        ("担当別", "staff"),
        ("部門別", "category"),
        ("日別", "day")
    ];

    private readonly IPopupNavigator popupNavigator;

    private readonly Session session;

    private readonly NetworkService network;

    private int period;

    private int scope = 1;

    private int group;

    [ObservableProperty]
    public partial string PeriodText { get; set; } = Periods[0];

    [ObservableProperty]
    public partial string ScopeText { get; set; } = Scopes[1];

    [ObservableProperty]
    public partial string GroupText { get; set; } = Groups[0].Name;

    // 読込中 / 取得できない / 結果 (空文字) を切り替える
    [ObservableProperty]
    public partial string CurrentState { get; set; } = ViewHelper.LoadingState;

    [ObservableProperty]
    public partial string Message { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string RangeText { get; set; } = string.Empty;

    // 純売上 (画面で数え上げて見せる)
    [ObservableProperty]
    public partial double NetSales { get; set; }

    [ObservableProperty]
    public partial ReportTotals Totals { get; set; } = ReportTotals.Empty;

    // 自端末は集計軸を選べないので内訳を出さない
    [ObservableProperty]
    public partial bool HasGroups { get; set; }

    [ObservableProperty]
    public partial string GroupTitle { get; set; } = string.Empty;

    public ObservableCollection<ReportGroupRow> GroupRows { get; } = [];

    public IObserveCommand PeriodCommand { get; }

    public IObserveCommand ScopeCommand { get; }

    public IObserveCommand GroupCommand { get; }

    public SalesReportViewModel(
        IPopupNavigator popupNavigator,
        Session session,
        NetworkService network)
    {
        this.popupNavigator = popupNavigator;
        this.session = session;
        this.network = network;

        PeriodCommand = MakeAsyncCommand(ChoosePeriodAsync);
        ScopeCommand = MakeAsyncCommand(ChooseScopeAsync);
        GroupCommand = MakeAsyncCommand(async () =>
        {
            var index = await popupNavigator.ChooseAsync(Groups.Select(static x => x.Name).ToArray(), "集計", group);
            if (index >= 0)
            {
                group = index;
                GroupText = Groups[index].Name;
                await LoadAsync();
            }
        });
    }

    public override Task OnNavigatedToAsync(INavigationContext context) => Navigator.PostActionAsync(LoadAsync).AsTask();

    private async Task ChoosePeriodAsync()
    {
        var index = await popupNavigator.ChooseAsync(Periods, "期間", period);
        if (index >= 0)
        {
            period = index;
            PeriodText = Periods[index];
            await LoadAsync();
        }
    }

    private async Task ChooseScopeAsync()
    {
        var index = await popupNavigator.ChooseAsync(Scopes, "範囲", scope);
        if (index >= 0)
        {
            scope = index;
            ScopeText = Scopes[index];
            await LoadAsync();
        }
    }

    private (DateOnly From, DateOnly To) ResolvePeriod()
    {
        var today = session.BusinessDate;
        return period switch
        {
            1 => (today.AddDays(-1), today.AddDays(-1)),
            2 => (today.AddDays(-(((int)today.DayOfWeek + 6) % 7)), today),
            3 => (new DateOnly(today.Year, today.Month, 1), today),
            _ => (today, today)
        };
    }

    private async Task LoadAsync()
    {
        var (from, to) = ResolvePeriod();
        var storeId = scope == 2 ? null : session.StoreId;

        // 合計は日別集計の Total (支払方法別などは値引・税を持たない)。自端末は端末別集計から自分の行を取り出す (集計軸は選べない)
        var totalGroupBy = scope == 0 ? "terminal" : "day";
        var result = await network.ExecuteAsync(h => h.GetSalesSummaryAsync(storeId, from, to, totalGroupBy));
        if (!result.IsSuccess)
        {
            ShowOffline();
            return;
        }

        var total = scope == 0
            ? result.Content!.Rows.FirstOrDefault(x => x.Key == session.TerminalId?.ToString()) ?? new ReportSalesSummaryResponseRow { Key = string.Empty, Label = string.Empty }
            : result.Content!.Total;
        var summary = result.Content;
        if ((scope != 0) && (Groups[group].GroupBy != totalGroupBy))
        {
            var rows = await network.ExecuteAsync(h => h.GetSalesSummaryAsync(storeId, from, to, Groups[group].GroupBy));
            if (!rows.IsSuccess)
            {
                ShowOffline();
                return;
            }

            summary = rows.Content!;
        }

        RangeText = $"{ViewHelper.Date(from)} 〜 {ViewHelper.Date(to)}  {Scopes[scope]}";
        NetSales = (double)total.NetSales;
        Totals = new ReportTotals(
            $"{total.TransactionCount} 件",
            ViewHelper.Yen(total.SalesTotal),
            $"{total.ReturnCount} 件",
            ViewHelper.Yen(total.ReturnsTotal),
            ViewHelper.Yen(total.DiscountTotal),
            ViewHelper.Yen(total.TaxTotal),
            $"{total.CustomerCount} 件",
            total.PointsEarned.ToString("#,##0", CultureInfo.InvariantCulture),
            total.PointsRedeemed.ToString("#,##0", CultureInfo.InvariantCulture));

        HasGroups = scope != 0;
        if (HasGroups)
        {
            GroupTitle = Groups[group].Name;
            var sum = summary.Rows.Sum(static x => Math.Max(0m, x.NetSales));
            GroupRows.Replace(summary.Rows.Select(x =>
            {
                var ratio = sum > 0m ? Math.Max(0m, x.NetSales) / sum : 0m;
                return new ReportGroupRow(x.Label, $"{x.TransactionCount} 件  {ViewHelper.Percent(Math.Round(ratio, 3))}", ViewHelper.Yen(x.NetSales), (double)ratio);
            }));
        }
        else
        {
            GroupRows.Clear();
        }

        CurrentState = string.Empty;
    }

    private void ShowOffline()
    {
        Message = "取得できませんでした。\nオンラインで「更新」してください。";
        CurrentState = ViewHelper.OfflineState;
    }

    protected override Task OnNotifyBackAsync() => Navigator.ForwardAsync(ViewId.Menu);

    protected override Task OnNotifyFunction1() => OnNotifyBackAsync();

    protected override Task OnNotifyFunction2() => ChoosePeriodAsync();

    protected override Task OnNotifyFunction3() => ChooseScopeAsync();

    protected override Task OnNotifyFunction4() => LoadAsync();
}
