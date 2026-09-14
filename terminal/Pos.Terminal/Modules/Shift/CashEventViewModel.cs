namespace Pos.Terminal.Modules.Shift;

// 入出金: 種別・金額・理由を ShiftUsecase で登録する (Outbox 経由でサーバへ)
public sealed partial class CashEventViewModel : AppViewModelBase
{
    private readonly IDialog dialog;

    private readonly IPopupNavigator popupNavigator;

    private readonly Session session;

    private readonly ShiftUsecase shifts;

    private decimal amount;

    // 種別ごとの表示 (選択状態・金額の可否) は画面側の Trigger で切り替える
    [ObservableProperty]
    public partial CashEventType Type { get; set; } = CashEventType.PaidIn;

    [ObservableProperty]
    public partial string AmountText { get; set; } = DisplayText.Yen(0);

    public EntryController Reason { get; } = new();

    public IObserveCommand SelectTypeCommand { get; }

    public IObserveCommand InputAmountCommand { get; }

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
    }

    private void SelectType(string value)
    {
        Type = Enum.Parse<CashEventType>(value);
        if (Type == CashEventType.NoSale)
        {
            amount = 0;
            AmountText = DisplayText.Yen(0);
        }
    }

    private async Task InputAmountAsync()
    {
        var text = await popupNavigator.InputNumberAsync("金額", amount.ToString("0", CultureInfo.InvariantCulture), 8);
        if ((text is not null) && Decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out var value))
        {
            amount = value;
            AmountText = DisplayText.Yen(value);
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

        if (!await dialog.AskAsync($"{DisplayText.Name(Type)} {DisplayText.Yen(amount)} を登録しますか？", null, "確定"))
        {
            return;
        }

        await shifts.AddCashEventAsync(Type, amount, Reason.Text.TrimToNull());
        await dialog.Toast($"{DisplayText.Name(Type)}を登録しました。");
        await Navigator.ForwardAsync(ViewId.Menu);
    }
}
