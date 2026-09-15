namespace Pos.Terminal.Modules.Shift;

// レジ開設: 営業日・釣銭準備金・担当でシフトを開き、ShiftUsecase で登録する (Outbox 経由でサーバへ)
public sealed partial class ShiftOpenViewModel : AppViewModelBase
{
    private readonly IDialog dialog;

    private readonly IPopupNavigator popupNavigator;

    private readonly Session session;

    private readonly ShiftUsecase shifts;

    private ViewId returnTo = ViewId.Menu;

    private decimal openingCash;

    [ObservableProperty]
    public partial string BusinessDateText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string StaffName { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string OpeningCashText { get; set; } = ViewHelper.Yen(0);

    public IObserveCommand InputCashCommand { get; }

    public ShiftOpenViewModel(
        IDialog dialog,
        IPopupNavigator popupNavigator,
        Session session,
        ShiftUsecase shifts)
    {
        this.dialog = dialog;
        this.popupNavigator = popupNavigator;
        this.session = session;
        this.shifts = shifts;

        InputCashCommand = MakeAsyncCommand(InputCashAsync);
    }

    public override async Task OnNavigatedToAsync(INavigationContext context)
    {
        returnTo = context.Parameter.GetReturnTo(ViewId.Menu);
        BusinessDateText = ViewHelper.Date(session.BusinessDate);
        StaffName = session.Staff?.Name ?? string.Empty;
        await Navigator.PostActionAsync(AdoptAsync);
    }

    // サーバに開設中のシフトが残っていれば引き継ぐ (再インストール時など)
    private async Task AdoptAsync()
    {
        if (await shifts.AdoptServerShiftAsync() is not null)
        {
            await dialog.InformationAsync("サーバに開設中のシフトがあるため引き継ぎました。");
            await Navigator.ForwardAsync(returnTo);
        }
    }

    private async Task InputCashAsync()
    {
        var text = await popupNavigator.InputAmountAsync("釣銭準備金", openingCash);
        if ((text is not null) && Decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out var value))
        {
            openingCash = value;
            OpeningCashText = ViewHelper.Yen(value);
        }
    }

    protected override Task OnNotifyBackAsync() => Navigator.ForwardAsync(ViewId.Menu);

    protected override Task OnNotifyFunction1() => OnNotifyBackAsync();

    protected override async Task OnNotifyFunction4()
    {
        if ((session.Store is null) || (session.Terminal is null) || (session.Staff is null))
        {
            return;
        }

        if (!await dialog.AskAsync($"釣銭準備金 {ViewHelper.Yen(openingCash)} でレジを開設しますか？", null, "開設"))
        {
            return;
        }

        await shifts.OpenAsync(openingCash);
        await Navigator.ForwardAsync(returnTo);
    }
}
