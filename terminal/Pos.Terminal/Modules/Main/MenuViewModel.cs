namespace Pos.Terminal.Modules.Main;

// T-02 ホーム: 機能選択。シフト未開設なら販売・返品・入出金は開設へ誘導する
public sealed partial class MenuViewModel : AppViewModelBase
{
    private static readonly Color OpenColor = Color.FromArgb("#43A047");

    private static readonly Color ClosedColor = Color.FromArgb("#9E9E9E");

    private readonly IDialog dialog;

    private readonly Session session;

    private readonly SyncWorker syncWorker;

    [ObservableProperty]
    public partial Version Version { get; set; }

    [ObservableProperty]
    public partial string ShiftText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial Color ShiftColor { get; set; } = ClosedColor;

    [ObservableProperty]
    public partial string ShiftDetail { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string ShiftButtonText { get; set; } = string.Empty;

    public IObserveCommand ForwardCommand { get; }

    public MenuViewModel(
        IAppInfo appInfo,
        IDialog dialog,
        Session session,
        SyncWorker syncWorker)
    {
        this.dialog = dialog;
        this.session = session;
        this.syncWorker = syncWorker;

        Version = appInfo.Version;

        ForwardCommand = MakeAsyncCommand<ViewId>(NavigateAsync);
    }

    public override Task OnNavigatedToAsync(INavigationContext context)
    {
        var shift = session.CurrentShift;
        if (shift is { Status: ShiftStatus.Open })
        {
            ShiftText = "開設中";
            ShiftColor = OpenColor;
            ShiftDetail = $"営業日 {DisplayText.Date(shift.BusinessDate)}  {DisplayText.Time(shift.OpenedAt)} 開設  担当 {session.Staff?.Name}";
            ShiftButtonText = "🔒 精算";
        }
        else
        {
            ShiftText = "未開設";
            ShiftColor = ClosedColor;
            ShiftDetail = $"担当 {session.Staff?.Name}";
            ShiftButtonText = "🔓 レジ開設";
        }

        syncWorker.Trigger();
        return Task.CompletedTask;
    }

    private async Task NavigateAsync(ViewId id)
    {
        switch (id)
        {
            case ViewId.Sales:
            case ViewId.Return:
            case ViewId.CashEvent:
                if (!session.IsShiftOpen)
                {
                    if (await dialog.AskAsync("レジが開設されていません。開設しますか？", null, "開設"))
                    {
                        await Navigator.ForwardAsync(ViewId.ShiftOpen, Parameters.Make().WithReturnTo(id));
                    }

                    return;
                }

                break;

            case ViewId.ShiftOpen:
                if (session.IsShiftOpen)
                {
                    await Navigator.ForwardAsync(ViewId.ShiftClose);
                    return;
                }

                break;
        }

        await Navigator.ForwardAsync(id);
    }

    protected override Task OnNotifyBackAsync()
    {
        AndroidHelper.MoveTaskToBack();
        return Task.CompletedTask;
    }
}
