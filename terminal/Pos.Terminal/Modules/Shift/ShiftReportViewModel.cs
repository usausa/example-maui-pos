namespace Pos.Terminal.Modules.Shift;

// 精算レポート: 送信済みならサーバの集計、未送信があれば端末の集計を表示する
public sealed partial class ShiftReportViewModel : AppViewModelBase
{
    private readonly IDialog dialog;

    private readonly Session session;

    private readonly ShiftUsecase shifts;

    private bool loaded;

    [ObservableProperty]
    public partial string HeaderText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string SourceText { get; set; } = string.Empty;

    public ObservableCollection<SummarySection> Sections { get; } = [];

    public ShiftReportViewModel(
        IDialog dialog,
        Session session,
        ShiftUsecase shifts)
    {
        this.dialog = dialog;
        this.session = session;
        this.shifts = shifts;
    }

    public override async Task OnNavigatedToAsync(INavigationContext context)
    {
        var shiftId = context.Parameter.GetShiftId() ?? session.CurrentShift?.Id;
        if (shiftId is null)
        {
            await Navigator.PostForwardAsync(ViewId.Menu);
            return;
        }

        await Navigator.PostActionAsync(() => LoadAsync(shiftId.Value));
    }

    private async Task LoadAsync(Guid shiftId)
    {
        var shift = await shifts.QueryAsync(shiftId);
        if (shift is null)
        {
            await Navigator.ForwardAsync(ViewId.Menu);
            return;
        }

        HeaderText = $"営業日 {ViewHelper.Date(shift.BusinessDate)}  {ViewHelper.Time(shift.OpenedAt)} 〜 {(shift.ClosedAt is null ? string.Empty : ViewHelper.Time(shift.ClosedAt.Value))}";

        var (summary, fromServer) = await shifts.QuerySummaryAsync(shift);
        SourceText = fromServer ? "☁️ サーバの集計" : session.UnsentCount > 0 ? "📱 端末の集計 (未送信あり)" : "📱 端末の集計 (オフライン)";

        var totals = summary.Shift.Totals;
        var cash = summary.Cash;
        var cashRows = new List<SummaryRow>
        {
            new("釣銭準備金", ViewHelper.Yen(cash.OpeningCash)),
            new("現金売上", ViewHelper.Yen(cash.CashSales)),
            new("現金返品", ViewHelper.MinusYen(cash.CashReturns)),
            new("入金", ViewHelper.Yen(cash.PaidIn)),
            new("出金", ViewHelper.MinusYen(cash.PaidOut))
        };

        // 前受金は現金で受け取った・返したシフトだけ
        if ((cash.DepositCashIn != 0m) || (cash.DepositCashOut != 0m))
        {
            cashRows.Add(new SummaryRow("前受金 受取", ViewHelper.Yen(cash.DepositCashIn)));
            cashRows.Add(new SummaryRow("前受金 返金", ViewHelper.MinusYen(cash.DepositCashOut)));
        }

        cashRows.Add(new SummaryRow("予想現金", ViewHelper.Yen(cash.ExpectedCash ?? 0m)));
        cashRows.Add(new SummaryRow("実査金額", cash.ActualCash is null ? "-" : ViewHelper.Yen(cash.ActualCash.Value)));
        cashRows.Add(new SummaryRow("過不足", cash.Difference is null ? "-" : ViewHelper.SignedYen(cash.Difference.Value)));
        Sections.Replace(
        [
            new SummarySection("💴 現金", cashRows),
            new SummarySection("🧾 取引",
            [
                new SummaryRow("販売", $"{totals.SalesCount} 件  {ViewHelper.Yen(totals.SalesTotal)}"),
                new SummaryRow("返品", $"{totals.ReturnCount} 件  {ViewHelper.Yen(totals.ReturnsTotal)}"),
                new SummaryRow("取消", $"{totals.VoidCount} 件")
            ]),
            new SummarySection("💳 支払方法別", summary.ByPaymentMethod.Select(static x => new SummaryRow(x.Name, $"{ViewHelper.Yen(x.SalesAmount - x.ReturnAmount)} ({x.SalesCount}/{x.ReturnCount})")).ToList()),
            new SummarySection("🧮 税率別", summary.ByTaxRate.Select(static x => new SummaryRow($"{(x.TaxIncluded ? "内税" : "外税")} {ViewHelper.Percent(x.Rate)}", $"対象 {ViewHelper.Yen(x.TaxableAmount)}  税 {ViewHelper.Yen(x.TaxAmount)}")).ToList()),
            new SummarySection("🗂️ 部門別", summary.ByCategory.Select(static x => new SummaryRow(x.Name, $"{ViewHelper.Quantity(x.Quantity)} 点  {ViewHelper.Yen(x.NetAmount)}")).ToList()),
            new SummarySection("🎁 ポイント",
            [
                new SummaryRow("付与", ViewHelper.Points(summary.Points.Earned)),
                new SummaryRow("利用", ViewHelper.Points(summary.Points.Redeemed))
            ])
        ]);
        loaded = true;
    }

    protected override Task OnNotifyBackAsync() => Navigator.ForwardAsync(ViewId.Menu);

    protected override Task OnNotifyFunction1() => OnNotifyBackAsync();

    protected override Task OnNotifyFunction2() =>
        loaded
            ? Share.Default.RequestAsync(new ShareTextRequest { Title = "精算レポート", Text = ShiftReportTextBuilder.Build(HeaderText, SourceText, Sections) })
            : Task.CompletedTask;

    // 印刷は Bluetooth ラインプリンタを前提にしていて、まだ作っていない
    protected override async Task OnNotifyFunction3() => await dialog.InformationAsync("印刷は未実装です。");

    protected override Task OnNotifyFunction4() => Navigator.ForwardAsync(ViewId.Menu);
}
