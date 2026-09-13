namespace Pos.Terminal.Modules.Shift;

using Pos.Shared.Shifts;

// T-52 精算レポート: 送信済みならサーバの集計、未送信があれば端末の集計を表示する
public sealed partial class ShiftReportViewModel : AppViewModelBase
{
    private readonly DataAccessor accessor;

    private readonly Session session;

    private readonly NetworkOperator network;

    private ShiftSummaryResponse? summary;

    [ObservableProperty]
    public partial string HeaderText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string SourceText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial IReadOnlyList<SummarySection> Sections { get; set; } = [];

    public ShiftReportViewModel(
        DataAccessor accessor,
        Session session,
        NetworkOperator network)
    {
        this.accessor = accessor;
        this.session = session;
        this.network = network;
    }

    public override async Task OnNavigatedToAsync(INavigationContext context)
    {
        var shiftId = context.Parameter.GetShiftId() ?? session.CurrentShift?.Id;
        var shift = shiftId is null ? null : await accessor.QueryShiftAsync(shiftId.Value);
        if (shift is null)
        {
            await Navigator.ForwardAsync(ViewId.Menu);
            return;
        }

        HeaderText = $"営業日 {DisplayText.Date(shift.BusinessDate)}  {DisplayText.Time(shift.OpenedAt)} 〜 {(shift.ClosedAt is null ? string.Empty : DisplayText.Time(shift.ClosedAt.Value))}";

        // 未送信がなければサーバの集計を使う
        if ((session.UnsentCount == 0) && network.IsConnected)
        {
            var result = await network.ExecuteAsync(h => h.GetShiftSummaryAsync(shift.Id), notify: false);
            if (result.IsSuccess)
            {
                summary = result.Content;
                SourceText = "☁ サーバの集計";
            }
        }

        if (summary is null)
        {
            summary = await ShiftCloseViewModel.BuildSummaryAsync(accessor, shift);
            SourceText = session.UnsentCount > 0 ? "📱 端末の集計 (未送信あり)" : "📱 端末の集計 (オフライン)";
        }

        var totals = summary.Shift.Totals;
        var cash = summary.Cash;
        var sections = new List<SummarySection>
        {
            new("💴 現金",
            [
                new SummaryRow("釣銭準備金", DisplayText.Yen(cash.OpeningCash)),
                new SummaryRow("現金売上", DisplayText.Yen(cash.CashSales)),
                new SummaryRow("現金返品", DisplayText.MinusYen(cash.CashReturns)),
                new SummaryRow("入金", DisplayText.Yen(cash.PaidIn)),
                new SummaryRow("出金", DisplayText.MinusYen(cash.PaidOut)),
                new SummaryRow("予想現金", DisplayText.Yen(cash.ExpectedCash ?? 0m)),
                new SummaryRow("実査金額", cash.ActualCash is null ? "-" : DisplayText.Yen(cash.ActualCash.Value)),
                new SummaryRow("過不足", cash.Difference is null ? "-" : cash.Difference.Value > 0 ? "+" + DisplayText.Yen(cash.Difference.Value) : DisplayText.Yen(cash.Difference.Value))
            ]),
            new("🧾 取引",
            [
                new SummaryRow("販売", $"{totals.SalesCount} 件  {DisplayText.Yen(totals.SalesTotal)}"),
                new SummaryRow("返品", $"{totals.ReturnCount} 件  {DisplayText.Yen(totals.ReturnsTotal)}"),
                new SummaryRow("取消", $"{totals.VoidCount} 件")
            ]),
            new("💳 支払方法別", summary.ByPaymentMethod.Select(static x => new SummaryRow(x.Name, $"{DisplayText.Yen(x.SalesAmount - x.ReturnAmount)} ({x.SalesCount}/{x.ReturnCount})")).ToList()),
            new("🧮 税率別", summary.ByTaxRate.Select(static x => new SummaryRow($"{(x.TaxIncluded ? "内税" : "外税")} {DisplayText.Percent(x.Rate)}", $"対象 {DisplayText.Yen(x.TaxableAmount)}  税 {DisplayText.Yen(x.TaxAmount)}")).ToList()),
            new("🗂 部門別", summary.ByCategory.Select(static x => new SummaryRow(x.Name, $"{DisplayText.Quantity(x.Quantity)} 点  {DisplayText.Yen(x.NetAmount)}")).ToList()),
            new("🎁 ポイント",
            [
                new SummaryRow("付与", DisplayText.Points(summary.Points.Earned)),
                new SummaryRow("利用", DisplayText.Points(summary.Points.Redeemed))
            ])
        };
        Sections = sections;
    }

    protected override Task OnNotifyBackAsync() => Navigator.ForwardAsync(ViewId.Menu);

    protected override Task OnNotifyFunction1() => OnNotifyBackAsync();

    protected override Task OnNotifyFunction2()
    {
        if (summary is null)
        {
            return Task.CompletedTask;
        }

        var sb = new StringBuilder();
        sb.AppendLine("精算レポート").AppendLine(HeaderText).AppendLine(SourceText);
        foreach (var section in Sections)
        {
            sb.AppendLine().AppendLine(section.Title);
            foreach (var row in section.Rows)
            {
                sb.Append(row.Label).Append(": ").AppendLine(row.Value);
            }
        }

        return Share.Default.RequestAsync(new ShareTextRequest { Title = "精算レポート", Text = sb.ToString() });
    }

    protected override Task OnNotifyFunction4() => Navigator.ForwardAsync(ViewId.Menu);
}
