namespace Pos.Terminal.Modules.Shift;

// 精算レポート: 送信済みならサーバの集計、未送信があれば端末の集計を表示する
public sealed partial class ShiftReportViewModel : AppViewModelBase
{
    private readonly Session session;

    private readonly ShiftUsecase shifts;

    private bool loaded;

    [ObservableProperty]
    public partial string HeaderText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string SourceText { get; set; } = string.Empty;

    public ObservableCollection<SummarySection> Sections { get; } = [];

    public ShiftReportViewModel(
        Session session,
        ShiftUsecase shifts)
    {
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

        HeaderText = $"営業日 {DisplayText.Date(shift.BusinessDate)}  {DisplayText.Time(shift.OpenedAt)} 〜 {(shift.ClosedAt is null ? string.Empty : DisplayText.Time(shift.ClosedAt.Value))}";

        var (summary, fromServer) = await shifts.QuerySummaryAsync(shift);
        SourceText = fromServer ? "☁ サーバの集計" : session.UnsentCount > 0 ? "📱 端末の集計 (未送信あり)" : "📱 端末の集計 (オフライン)";

        var totals = summary.Shift.Totals;
        var cash = summary.Cash;
        Sections.Replace(
        [
            new SummarySection("💴 現金",
            [
                new SummaryRow("釣銭準備金", DisplayText.Yen(cash.OpeningCash)),
                new SummaryRow("現金売上", DisplayText.Yen(cash.CashSales)),
                new SummaryRow("現金返品", DisplayText.MinusYen(cash.CashReturns)),
                new SummaryRow("入金", DisplayText.Yen(cash.PaidIn)),
                new SummaryRow("出金", DisplayText.MinusYen(cash.PaidOut)),
                new SummaryRow("予想現金", DisplayText.Yen(cash.ExpectedCash ?? 0m)),
                new SummaryRow("実査金額", cash.ActualCash is null ? "-" : DisplayText.Yen(cash.ActualCash.Value)),
                new SummaryRow("過不足", cash.Difference is null ? "-" : DisplayText.SignedYen(cash.Difference.Value))
            ]),
            new SummarySection("🧾 取引",
            [
                new SummaryRow("販売", $"{totals.SalesCount} 件  {DisplayText.Yen(totals.SalesTotal)}"),
                new SummaryRow("返品", $"{totals.ReturnCount} 件  {DisplayText.Yen(totals.ReturnsTotal)}"),
                new SummaryRow("取消", $"{totals.VoidCount} 件")
            ]),
            new SummarySection("💳 支払方法別", summary.ByPaymentMethod.Select(static x => new SummaryRow(x.Name, $"{DisplayText.Yen(x.SalesAmount - x.ReturnAmount)} ({x.SalesCount}/{x.ReturnCount})")).ToList()),
            new SummarySection("🧮 税率別", summary.ByTaxRate.Select(static x => new SummaryRow($"{(x.TaxIncluded ? "内税" : "外税")} {DisplayText.Percent(x.Rate)}", $"対象 {DisplayText.Yen(x.TaxableAmount)}  税 {DisplayText.Yen(x.TaxAmount)}")).ToList()),
            new SummarySection("🗂 部門別", summary.ByCategory.Select(static x => new SummaryRow(x.Name, $"{DisplayText.Quantity(x.Quantity)} 点  {DisplayText.Yen(x.NetAmount)}")).ToList()),
            new SummarySection("🎁 ポイント",
            [
                new SummaryRow("付与", DisplayText.Points(summary.Points.Earned)),
                new SummaryRow("利用", DisplayText.Points(summary.Points.Redeemed))
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

    protected override Task OnNotifyFunction4() => Navigator.ForwardAsync(ViewId.Menu);
}
