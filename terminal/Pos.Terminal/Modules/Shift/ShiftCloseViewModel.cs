namespace Pos.Terminal.Modules.Shift;

using System.Text.Json;

using Pos.Shared.Shifts;
using Pos.Shared.Transactions;
using Pos.Terminal.Models.Entity;

using Smart.Data;

// T-51 精算: ローカルの取引・入出金から予想現金を出し、実査金額との過不足を確認してシフトを閉じる
public sealed partial class ShiftCloseViewModel : AppViewModelBase
{
    private static readonly Color EvenColor = Color.FromArgb("#1976D2");

    private static readonly Color DifferenceColorValue = Color.FromArgb("#E53935");

    private readonly IDialog dialog;

    private readonly IPopupNavigator popupNavigator;

    private readonly IDbProvider provider;

    private readonly DataAccessor accessor;

    private readonly Session session;

    private readonly SyncWorker syncWorker;

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
    public partial IReadOnlyList<SummaryRow> Rows { get; set; } = [];

    [ObservableProperty]
    public partial string ActualCashText { get; set; } = "未入力";

    [ObservableProperty]
    public partial string DifferenceText { get; set; } = "-";

    [ObservableProperty]
    public partial Color DifferenceColor { get; set; } = EvenColor;

    public IObserveCommand InputActualCommand { get; }

    public ShiftCloseViewModel(
        IDialog dialog,
        IPopupNavigator popupNavigator,
        IDbProvider provider,
        DataAccessor accessor,
        Session session,
        SyncWorker syncWorker)
    {
        this.dialog = dialog;
        this.popupNavigator = popupNavigator;
        this.provider = provider;
        this.accessor = accessor;
        this.session = session;
        this.syncWorker = syncWorker;

        InputActualCommand = MakeAsyncCommand(InputActualAsync);
    }

    public override async Task OnNavigatedToAsync(INavigationContext context)
    {
        shift = session.CurrentShift;
        if (shift is null)
        {
            await Navigator.ForwardAsync(ViewId.Menu);
            return;
        }

        HasUnsent = session.UnsentCount > 0;
        UnsentText = session.FailedCount > 0
            ? $"⚠ 未送信 {session.UnsentCount} 件 (要確認 {session.FailedCount} 件)。精算前に「未送信」で確認してください。"
            : $"⚠ 未送信 {session.UnsentCount} 件があります。送信完了を待ってから精算することを推奨します。";
        ShiftText = $"営業日 {DisplayText.Date(shift.BusinessDate)}  {DisplayText.Time(shift.OpenedAt)} 開設";

        var summary = await BuildSummaryAsync(accessor, shift);
        expectedCash = summary.Cash.ExpectedCash ?? 0m;
        var totals = summary.Shift.Totals;
        Rows =
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
        ];
        UpdateDifference();
    }

    // ローカルの取引 (Payload = TransactionResponse) と入出金から集計する
    public static async ValueTask<ShiftSummaryResponse> BuildSummaryAsync(DataAccessor accessor, LocalShiftEntity shift)
    {
        var transactions = (await accessor.QueryTransactionListAsync(shift.Id, null, null, 10000))
            .Select(static x => JsonSerializer.Deserialize<TransactionResponse>(x.Payload, HttpService.JsonOptions)!)
            .ToList();
        var cashEvents = await accessor.QueryCashEventListAsync(shift.Id);
        var paymentMethods = await accessor.QueryPaymentMethodListAsync();
        var categories = await accessor.QueryCategoryListAsync();
        return ShiftSummaryBuilder.Build(shift, transactions, cashEvents, paymentMethods, categories);
    }

    private void UpdateDifference()
    {
        if (actualCash is null)
        {
            DifferenceText = "-";
            DifferenceColor = EvenColor;
            return;
        }

        var difference = actualCash.Value - expectedCash;
        DifferenceText = difference == 0 ? "±¥0" : difference > 0 ? "+" + DisplayText.Yen(difference) : DisplayText.Yen(difference);
        DifferenceColor = difference == 0 ? EvenColor : DifferenceColorValue;
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
        if ((shift is null) || (session.Staff is null))
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

        var now = DateTime.UtcNow;
        var difference = actualCash.Value - expectedCash;
        var request = new ShiftCloseRequest
        {
            ClosedAt = now,
            ClosedByStaffId = session.Staff.Id,
            ActualCash = actualCash.Value,
            Denominations = denominations
        };

        await provider.UsingTxAsync(async (_, tx) =>
        {
            await accessor.CloseShiftAsync(tx, shift.Id, now, session.Staff.Id, actualCash.Value, expectedCash, difference, null);
            await accessor.InsertOutboxAsync(tx, SyncWorker.CreateEntry(OutboxKind.ShiftClose, shift.Id, request, now));
            await tx.CommitAsync();
        });

        session.CurrentShift = await accessor.QueryShiftAsync(shift.Id);
        await syncWorker.UpdateCountsAsync();
        syncWorker.Trigger();

        await Navigator.ForwardAsync(ViewId.ShiftReport, Parameters.Make().WithShiftId(shift.Id));
    }
}
