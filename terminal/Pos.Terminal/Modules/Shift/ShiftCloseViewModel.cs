namespace Pos.Terminal.Modules.Shift;

using Pos.Contract.Shifts;
using Pos.Terminal.Models.Entity;

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

    public ObservableCollection<SummaryRow> Rows { get; } = [];

    [ObservableProperty]
    public partial string ActualCashText { get; set; } = "未入力";

    [ObservableProperty]
    public partial string DifferenceText { get; set; } = "-";

    // 過不足があるとき (色は画面側の Converter で変える)
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
            ? $"⚠ 未送信 {session.UnsentCount} 件 (要確認 {session.FailedCount} 件)。精算前に「未送信」で確認してください。"
            : $"⚠ 未送信 {session.UnsentCount} 件があります。送信完了を待ってから精算することを推奨します。";
        ShiftText = $"営業日 {DisplayText.Date(shift.BusinessDate)}  {DisplayText.Time(shift.OpenedAt)} 開設";
        UpdateDifference();

        await Navigator.PostActionAsync(() => LoadAsync(shift));
    }

    private async Task LoadAsync(LocalShiftEntity target)
    {
        var summary = await shifts.BuildSummaryAsync(target);
        expectedCash = summary.Cash.ExpectedCash ?? 0m;
        var totals = summary.Shift.Totals;
        Rows.Replace(
        [
            new SummaryRow("🛒 販売", $"{totals.SalesCount} 件  {DisplayText.Yen(totals.SalesTotal)}"),
            new SummaryRow("↩ 返品", $"{totals.ReturnCount} 件  {DisplayText.Yen(totals.ReturnsTotal)}"),
            new SummaryRow("🚫 取消", $"{totals.VoidCount} 件"),
            new SummaryRow("釣銭準備金", DisplayText.Yen(summary.Cash.OpeningCash)),
            new SummaryRow("現金売上", DisplayText.Yen(summary.Cash.CashSales)),
            new SummaryRow("現金返品", DisplayText.MinusYen(summary.Cash.CashReturns)),
            new SummaryRow("入金", DisplayText.Yen(summary.Cash.PaidIn)),
            new SummaryRow("出金", DisplayText.MinusYen(summary.Cash.PaidOut)),
            new SummaryRow("予想現金", DisplayText.Yen(expectedCash))
        ]);
        UpdateDifference();
    }

    private void UpdateDifference()
    {
        if (actualCash is null)
        {
            DifferenceText = "-";
            HasDifference = false;
            return;
        }

        var difference = actualCash.Value - expectedCash;
        DifferenceText = DisplayText.SignedYen(difference);
        HasDifference = difference != 0;
    }

    private async Task InputActualAsync()
    {
        var text = await popupNavigator.InputNumberAsync("実査金額", (actualCash ?? 0m).ToString("0", CultureInfo.InvariantCulture), 9);
        if ((text is not null) && Decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out var value))
        {
            actualCash = value;
            denominations = [];
            ActualCashText = DisplayText.Yen(value);
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
            ActualCashText = DisplayText.Yen(result.Total);
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

        var message = $"予想現金 {DisplayText.Yen(expectedCash)}\n実査金額 {DisplayText.Yen(actualCash.Value)}\n過不足 {DifferenceText}\n精算しますか？";
        if (HasUnsent)
        {
            message = $"⚠ 未送信 {session.UnsentCount} 件があります。精算後も送信は続きます。\n\n" + message;
        }

        if (!await dialog.AskAsync(message, "精算", "精算"))
        {
            return;
        }

        await shifts.CloseAsync(shift, actualCash.Value, expectedCash, denominations);
        await Navigator.ForwardAsync(ViewId.ShiftReport, Parameters.Make().WithShiftId(shift.Id));
    }
}
