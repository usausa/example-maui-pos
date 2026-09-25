namespace Pos.Terminal.Modules.Shift;

using Pos.Contract.Shifts;
using Pos.Terminal.Models.Entity;

// シフトの件数 (表示用の文字列)
public sealed record ShiftCounts(string SalesCount, string SalesTotal, string ReturnCount, string ReturnsTotal, string VoidCount)
{
    public static ShiftCounts Empty { get; } = new("-", "-", "-", "-", "-");
}

// 精算: ローカルの取引・入出金から予想現金を出し、実査金額との過不足を確認してシフトを閉じる
public sealed partial class ShiftCloseViewModel : AppViewModelBase
{
    private readonly IDialog dialog;

    private readonly IPopupNavigator popupNavigator;

    private readonly Session session;

    private readonly ShiftUsecase shifts;

    private LocalShiftEntity? shift;

    private decimal expectedCash;

    private decimal? actualCash;

    private IReadOnlyList<ShiftCloseRequestDenomination> denominations = [];

    [ObservableProperty]
    public partial bool HasUnsent { get; set; }

    [ObservableProperty]
    public partial string UnsentText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string ShiftText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial ShiftCounts Counts { get; set; } = ShiftCounts.Empty;

    // 現金の内訳 (釣銭準備金・現金売上・現金返品・入金・出金)
    public ObservableCollection<SummaryRow> CashRows { get; } = [];

    [ObservableProperty]
    public partial string ExpectedCashText { get; set; } = "-";

    [ObservableProperty]
    public partial string ActualCashText { get; set; } = "未入力";

    [ObservableProperty]
    public partial string DifferenceText { get; set; } = "-";

    // 実査金額が予想現金と一致 / 過不足あり (色は画面側のトリガーで変える。未入力はどちらも false)
    [ObservableProperty]
    public partial bool IsMatched { get; set; }

    [ObservableProperty]
    public partial bool HasDifference { get; set; }

    public IObserveCommand InputActualCommand { get; }

    public ShiftCloseViewModel(
        IDialog dialog,
        IPopupNavigator popupNavigator,
        Session session,
        ShiftUsecase shifts)
    {
        this.dialog = dialog;
        this.popupNavigator = popupNavigator;
        this.session = session;
        this.shifts = shifts;

        InputActualCommand = MakeAsyncCommand(InputActualAsync);
    }

    public override async Task OnNavigatedToAsync(INavigationContext context)
    {
        shift = session.CurrentShift;
        if (shift is null)
        {
            await Navigator.PostForwardAsync(ViewId.Menu);
            return;
        }

        HasUnsent = session.UnsentCount > 0;
        UnsentText = session.FailedCount > 0
            ? $"⚠️ 未送信 {session.UnsentCount} 件 (要確認 {session.FailedCount} 件)。精算前に「未送信」で確認してください。"
            : $"⚠️ 未送信 {session.UnsentCount} 件があります。送信完了を待ってから精算することを推奨します。";
        ShiftText = $"営業日 {ViewHelper.Date(shift.BusinessDate)}  {ViewHelper.Time(shift.OpenedAt)} 開設";
        UpdateDifference();

        await Navigator.PostActionAsync(() => LoadAsync(shift));
    }

    private async Task LoadAsync(LocalShiftEntity target)
    {
        var summary = await shifts.BuildSummaryAsync(target);
        expectedCash = summary.Cash.ExpectedCash ?? 0m;
        var totals = summary.Shift.Totals;
        Counts = new ShiftCounts(
            $"{totals.SalesCount} 件",
            ViewHelper.Yen(totals.SalesTotal),
            $"{totals.ReturnCount} 件",
            ViewHelper.Yen(totals.ReturnsTotal),
            $"{totals.VoidCount} 件");
        var rows = new List<SummaryRow>
        {
            new("釣銭準備金", ViewHelper.Yen(summary.Cash.OpeningCash)),
            new("現金売上", ViewHelper.Yen(summary.Cash.CashSales)),
            new("現金返品", ViewHelper.MinusYen(summary.Cash.CashReturns)),
            new("入金", ViewHelper.Yen(summary.Cash.PaidIn)),
            new("出金", ViewHelper.MinusYen(summary.Cash.PaidOut))
        };

        // 前受金は現金で受け取った・返したシフトだけ
        if ((summary.Cash.DepositCashIn != 0m) || (summary.Cash.DepositCashOut != 0m))
        {
            rows.Add(new SummaryRow("前受金 受取", ViewHelper.Yen(summary.Cash.DepositCashIn)));
            rows.Add(new SummaryRow("前受金 返金", ViewHelper.MinusYen(summary.Cash.DepositCashOut)));
        }

        CashRows.Replace(rows);
        ExpectedCashText = ViewHelper.Yen(expectedCash);
        UpdateDifference();
    }

    private void UpdateDifference()
    {
        if (actualCash is null)
        {
            DifferenceText = "-";
            IsMatched = false;
            HasDifference = false;
            return;
        }

        var difference = actualCash.Value - expectedCash;
        DifferenceText = ViewHelper.SignedYen(difference);
        IsMatched = difference == 0;
        HasDifference = difference != 0;
    }

    private async Task InputActualAsync()
    {
        var text = await popupNavigator.InputCashAsync("実査金額", actualCash ?? 0m);
        if ((text is not null) && Decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out var value))
        {
            actualCash = value;
            denominations = [];
            ActualCashText = ViewHelper.Yen(value);
            UpdateDifference();
        }
    }

    protected override Task OnNotifyBackAsync() => Navigator.ForwardAsync(ViewId.Menu);

    protected override Task OnNotifyFunction1() => OnNotifyBackAsync();

    protected override async Task OnNotifyFunction2()
    {
        var result = await popupNavigator.PopupAsync<IReadOnlyList<ShiftCloseRequestDenomination>, DenominationsResult?>(DialogId.Denominations, denominations);
        if (result is not null)
        {
            actualCash = result.Total;
            denominations = result.Denominations;
            ActualCashText = ViewHelper.Yen(result.Total);
            UpdateDifference();
        }
    }

    protected override Task OnNotifyFunction3() =>
        Navigator.ForwardAsync(ViewId.Setting, Parameters.Make().WithReturnTo(ViewId.ShiftClose));

    protected override async Task OnNotifyFunction4()
    {
        if ((shift is null) || !session.CanTransact)
        {
            return;
        }

        if (actualCash is null)
        {
            await dialog.InformationAsync("実査金額を入力してください。");
            return;
        }

        var message = $"予想現金 {ViewHelper.Yen(expectedCash)}\n実査金額 {ViewHelper.Yen(actualCash.Value)}\n過不足 {DifferenceText}\n精算しますか？";
        if (HasUnsent)
        {
            message = $"⚠️ 未送信 {session.UnsentCount} 件があります。精算後も送信は続きます。\n\n" + message;
        }

        if (!await dialog.AskAsync(message, "精算", "精算"))
        {
            return;
        }

        await shifts.CloseAsync(shift, actualCash.Value, expectedCash, denominations);
        await Navigator.ForwardAsync(ViewId.ShiftReport, Parameters.Make().WithShiftId(shift.Id));
    }
}
