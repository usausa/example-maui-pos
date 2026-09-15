namespace Pos.Terminal.Modules.Shift;

using Pos.Terminal.Modules.Dialogs;

// 入出金: 種別・金額・理由を ShiftUsecase で登録する (Outbox 経由でサーバへ)
public sealed partial class CashEventViewModel : AppViewModelBase
{
    private static readonly ReasonItem[] Reasons =
    [
        new(null, "釣銭補充"),
        new(null, "両替"),
        new(null, "経費支払"),
        new(null, "売上金回収")
    ];

    private readonly IDialog dialog;

    private readonly IPopupNavigator popupNavigator;

    private readonly Session session;

    private readonly ShiftUsecase shifts;

    private decimal amount;

    // 種別ごとの表示 (選択状態・金額の可否) は画面側の Trigger で切り替える
    [ObservableProperty]
    public partial CashEventType Type { get; set; } = CashEventType.PaidIn;

    [ObservableProperty]
    public partial string AmountText { get; set; } = ViewHelper.Yen(0);

    [ObservableProperty]
    public partial string? ReasonText { get; set; }

    public IObserveCommand SelectTypeCommand { get; }

    public IObserveCommand InputAmountCommand { get; }

    public IObserveCommand SelectReasonCommand { get; }

    public CashEventViewModel(
        IDialog dialog,
        IPopupNavigator popupNavigator,
        Session session,
        ShiftUsecase shifts)
    {
        this.dialog = dialog;
        this.popupNavigator = popupNavigator;
        this.session = session;
        this.shifts = shifts;

        SelectTypeCommand = MakeDelegateCommand<string>(SelectType);
        InputAmountCommand = MakeAsyncCommand(InputAmountAsync);
        SelectReasonCommand = MakeAsyncCommand(SelectReasonAsync);
    }

    private void SelectType(string value)
    {
        Type = Enum.Parse<CashEventType>(value);
        if (Type == CashEventType.NoSale)
        {
            amount = 0;
            AmountText = ViewHelper.Yen(0);
        }
    }

    private async Task InputAmountAsync()
    {
        var text = await popupNavigator.InputAmountAsync("金額", amount);
        if ((text is not null) && Decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out var value))
        {
            amount = value;
            AmountText = ViewHelper.Yen(value);
        }
    }

    private async Task SelectReasonAsync()
    {
        var reason = await popupNavigator.PopupAsync<ReasonSelectParameter, ReasonSelectResult?>(DialogId.ReasonSelect, new ReasonSelectParameter("理由", Reasons, true));
        if (reason is not null)
        {
            ReasonText = reason.Text;
        }
    }

    protected override Task OnNotifyBackAsync() => Navigator.ForwardAsync(ViewId.Menu);

    protected override Task OnNotifyFunction1() => OnNotifyBackAsync();

    protected override async Task OnNotifyFunction4()
    {
        if (!session.CanTransact)
        {
            return;
        }

        if ((Type != CashEventType.NoSale) && (amount <= 0))
        {
            await dialog.InformationAsync("金額を入力してください。");
            return;
        }

        if (!await dialog.AskAsync($"{ViewHelper.Name(Type)} {ViewHelper.Yen(amount)} を登録しますか？", null, "確定"))
        {
            return;
        }

        await shifts.AddCashEventAsync(Type, amount, ReasonText);
        await dialog.Toast($"{ViewHelper.Name(Type)}を登録しました。");
        await Navigator.ForwardAsync(ViewId.Menu);
    }
}
