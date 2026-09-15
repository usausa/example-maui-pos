namespace Pos.Terminal.Modules.Main;

// ホーム: 機能選択。シフト未開設なら販売・返品・入出金は開設へ誘導する
public sealed partial class MenuViewModel : AppViewModelBase
{
    private readonly IDialog dialog;

    private readonly Session session;

    private readonly SyncService sync;

    [ObservableProperty]
    public partial Version Version { get; set; }

    // シフト状態の文言と色は画面側の Converter で付ける
    [ObservableProperty]
    public partial bool IsShiftOpen { get; set; }

    [ObservableProperty]
    public partial string ShiftDetail { get; set; } = string.Empty;

    public IObserveCommand ForwardCommand { get; }

    // 根の画面: 戻るはプラットフォームに任せる (タスクを背面へ)
    public override bool HandlesBack => false;

    public MenuViewModel(
        IAppInfo appInfo,
        IDialog dialog,
        Session session,
        SyncService sync)
    {
        this.dialog = dialog;
        this.session = session;
        this.sync = sync;

        Version = appInfo.Version;
        ForwardCommand = MakeAsyncCommand<ViewId>(NavigateAsync);
    }

    public override Task OnNavigatedToAsync(INavigationContext context)
    {
        var shift = session.CurrentShift;
        IsShiftOpen = session.IsShiftOpen;
        ShiftDetail = shift is not null && IsShiftOpen
            ? $"営業日 {ViewHelper.Date(shift.BusinessDate)}  {ViewHelper.Time(shift.OpenedAt)} 開設  担当 {session.Staff?.Name}"
            : $"担当 {session.Staff?.Name}";
        sync.Trigger();
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
}
