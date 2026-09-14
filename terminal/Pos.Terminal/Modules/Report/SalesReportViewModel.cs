namespace Pos.Terminal.Modules.Report;

using Pos.Contract.Reports;

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

    private readonly IDialog dialog;

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

    [ObservableProperty]
    public partial string Message { get; set; } = string.Empty;

    public ObservableCollection<SummarySection> Sections { get; } = [];

    public IObserveCommand PeriodCommand { get; }

    public IObserveCommand ScopeCommand { get; }

    public IObserveCommand GroupCommand { get; }

    public SalesReportViewModel(
        IDialog dialog,
        Session session,
        NetworkService network)
    {
        this.dialog = dialog;
        this.session = session;
        this.network = network;

        PeriodCommand = MakeAsyncCommand(ChoosePeriodAsync);
        ScopeCommand = MakeAsyncCommand(ChooseScopeAsync);
        GroupCommand = MakeAsyncCommand(async () =>
        {
            var index = await dialog.ChooseAsync(Groups.Select(static x => x.Name).ToArray(), "集計", group);
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
        var index = await dialog.ChooseAsync(Periods, "期間", period);
        if (index >= 0)
        {
            period = index;
            PeriodText = Periods[index];
            await LoadAsync();
        }
    }

    private async Task ChooseScopeAsync()
    {
        var index = await dialog.ChooseAsync(Scopes, "範囲", scope);
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
            Message = "取得できませんでした。オンラインで「更新」してください。";
            Sections.Clear();
            return;
        }

        var total = scope == 0
            ? result.Content!.Rows.FirstOrDefault(x => x.Key == session.TerminalId?.ToString()) ?? new SalesSummaryResponseRow { Key = string.Empty, Label = string.Empty }
            : result.Content!.Total;
        var summary = result.Content;
        if ((scope != 0) && (Groups[group].GroupBy != totalGroupBy))
        {
            var rows = await network.ExecuteAsync(h => h.GetSalesSummaryAsync(storeId, from, to, Groups[group].GroupBy));
            if (!rows.IsSuccess)
            {
                return;
            }

            summary = rows.Content!;
        }

        Message = $"📅 {DisplayText.Date(from)} 〜 {DisplayText.Date(to)}  {Scopes[scope]}";

        var sections = new List<SummarySection>
        {
            new("💰 合計",
            [
                new SummaryRow("純売上", DisplayText.Yen(total.NetSales)),
                new SummaryRow("売上", $"{total.TransactionCount} 件  {DisplayText.Yen(total.SalesTotal)}"),
                new SummaryRow("返品", $"{total.ReturnCount} 件  {DisplayText.Yen(total.ReturnsTotal)}"),
                new SummaryRow("値引", DisplayText.Yen(total.DiscountTotal)),
                new SummaryRow("消費税", DisplayText.Yen(total.TaxTotal)),
                new SummaryRow("会員取引", $"{total.CustomerCount} 件"),
                new SummaryRow("ポイント", $"付与 {total.PointsEarned:#,##0}  利用 {total.PointsRedeemed:#,##0}")
            ])
        };
        if (scope != 0)
        {
            sections.Add(new SummarySection("📊 " + Groups[group].Name, summary.Rows
                .Select(static x => new SummaryRow(x.Label, $"{x.TransactionCount} 件  {DisplayText.Yen(x.NetSales)}"))
                .DefaultIfEmpty(new SummaryRow("データなし", string.Empty))
                .ToList()));
        }

        Sections.Replace(sections);
    }

    protected override Task OnNotifyBackAsync() => Navigator.ForwardAsync(ViewId.Menu);

    protected override Task OnNotifyFunction1() => OnNotifyBackAsync();

    protected override Task OnNotifyFunction2() => ChoosePeriodAsync();

    protected override Task OnNotifyFunction3() => ChooseScopeAsync();

    protected override Task OnNotifyFunction4() => LoadAsync();
}
