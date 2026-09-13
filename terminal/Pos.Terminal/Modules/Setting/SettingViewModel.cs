namespace Pos.Terminal.Modules.Setting;

using Pos.Shared.Sync;
using Pos.Terminal.Models.Entity;

public sealed record OutboxItem(OutboxEntity Entity, string StatusText, Color StatusColor, string KindText, string TimeText, string Error);

// T-90 設定・同期: 端末情報、手動同期、未送信一覧 (要確認の再送・破棄)、ログイン後の画面、スタッフ切替、接続設定
public sealed partial class SettingViewModel : AppViewModelBase
{
    private static readonly Color PendingColor = Color.FromArgb("#FB8C00");

    private static readonly Color FailedColor = Color.FromArgb("#E53935");

    private static readonly Color OkColor = Color.FromArgb("#43A047");

    private readonly IDialog dialog;

    private readonly IAppInfo appInfo;

    private readonly DataAccessor accessor;

    private readonly Settings settings;

    private readonly Session session;

    private readonly SyncWorker syncWorker;

    private ViewId returnTo = ViewId.Menu;

    [ObservableProperty]
    public partial IReadOnlyList<SummaryRow> Info { get; set; } = [];

    [ObservableProperty]
    public partial string LastSyncText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string UnsentText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial Color UnsentColor { get; set; } = OkColor;

    [ObservableProperty]
    public partial bool OutboxVisible { get; set; }

    [ObservableProperty]
    public partial IReadOnlyList<OutboxItem> Outbox { get; set; } = [];

    [ObservableProperty]
    public partial bool OpenSalesAfterLogin { get; set; }

    public IObserveCommand OutboxCommand { get; }

    public IObserveCommand SwitchStaffCommand { get; }

    public IObserveCommand SetupCommand { get; }

    public SettingViewModel(
        IDialog dialog,
        IAppInfo appInfo,
        DataAccessor accessor,
        Settings settings,
        Session session,
        SyncWorker syncWorker)
    {
        this.dialog = dialog;
        this.appInfo = appInfo;
        this.accessor = accessor;
        this.settings = settings;
        this.session = session;
        this.syncWorker = syncWorker;

        OutboxCommand = MakeAsyncCommand<OutboxItem>(HandleOutboxAsync);
        SwitchStaffCommand = MakeAsyncCommand(() => Navigator.ForwardAsync(ViewId.StaffSelect));
        SetupCommand = MakeAsyncCommand(async () =>
        {
            if (await dialog.AskAsync("接続設定をやり直しますか？\n未送信の取引は残ります。", null, "設定へ"))
            {
                await Navigator.ForwardAsync(ViewId.Setup);
            }
        });

        Disposables.Add(session.PropertyChangedAsObservable().ObserveOnCurrentContext().Subscribe(_ => UpdateSync()));
        SubscribeOpenSalesAfterLogin(x => settings.OpenSalesAfterLogin = x);
    }

    public override async Task OnNavigatedToAsync(INavigationContext context)
    {
        returnTo = context.Parameter.GetReturnTo(ViewId.Menu);
        OpenSalesAfterLogin = settings.OpenSalesAfterLogin;
        Info =
        [
            new SummaryRow("店舗", session.Store is null ? "-" : $"{session.Store.Name} ({session.Store.Code})"),
            new SummaryRow("端末", session.Terminal is null ? "-" : $"{session.Terminal.Name} (No.{session.Terminal.TerminalNo})"),
            new SummaryRow("担当", session.Staff?.Name ?? "-"),
            new SummaryRow("サーバ", settings.ApiEndPoint),
            new SummaryRow("バージョン", appInfo.VersionString)
        ];
        UpdateSync();
        await LoadOutboxAsync();
    }

    private void UpdateSync()
    {
        LastSyncText = session.LastSyncAt is null ? "最終同期: -" : $"最終同期: {DisplayText.DateTime(session.LastSyncAt.Value)}";
        UnsentText = session.UnsentCount == 0 ? "未送信なし" : session.FailedCount > 0 ? $"未送信 {session.UnsentCount} / 要確認 {session.FailedCount}" : $"未送信 {session.UnsentCount}";
        UnsentColor = session.FailedCount > 0 ? FailedColor : session.UnsentCount > 0 ? PendingColor : OkColor;
    }

    private async ValueTask LoadOutboxAsync()
    {
        Outbox = (await accessor.QueryOutboxListAsync(null, 200)).Select(static x => new OutboxItem(
            x,
            x.Status == OutboxStatus.Failed ? "要確認" : "未送信",
            x.Status == OutboxStatus.Failed ? FailedColor : PendingColor,
            KindName(x.Kind),
            DisplayText.DateTime(x.CreatedAt),
            x.LastError ?? string.Empty)).ToList();
    }

    private static string KindName(OutboxKind kind) => kind switch
    {
        OutboxKind.ShiftOpen => "レジ開設",
        OutboxKind.Transaction => "取引",
        OutboxKind.TransactionVoid => "取引取消",
        OutboxKind.CashEvent => "入出金",
        OutboxKind.ShiftClose => "精算",
        OutboxKind.InventoryChanges => "在庫変動",
        _ => kind.ToString()
    };

    private async Task HandleOutboxAsync(OutboxItem item)
    {
        var entity = item.Entity;
        if (entity.Status != OutboxStatus.Failed)
        {
            await dialog.InformationAsync($"{item.KindText}\n{item.TimeText}\n試行 {entity.Attempts} 回\n{entity.LastError}", "未送信");
            return;
        }

        var index = await dialog.ChooseAsync(["🔁 再送する", "🗑 破棄する (サーバには送らない)", "ℹ 詳細"], item.KindText);
        switch (index)
        {
            case 0:
                await syncWorker.RetryAsync(entity.Id);
                await dialog.Toast("再送します。");
                break;
            case 1:
                if (await dialog.AskAsync("この送信を破棄しますか？\nサーバには反映されません。", "破棄", "破棄"))
                {
                    await syncWorker.DiscardAsync(entity.Id);
                }

                break;
            case 2:
                await dialog.InformationAsync($"{item.KindText}\n{item.TimeText}\n試行 {entity.Attempts} 回\n{entity.LastError}\n\n{entity.Payload}", "要確認");
                break;
        }

        await LoadOutboxAsync();
    }

    protected override Task OnNotifyBackAsync() => Navigator.ForwardAsync(returnTo);

    protected override Task OnNotifyFunction1() => OnNotifyBackAsync();

    protected override Task OnNotifyFunction2() =>
        Navigator.ForwardAsync(ViewId.Scan, Parameters.Make().WithScan(ScanMode.Setup, ViewId.Setup));

    protected override async Task OnNotifyFunction3()
    {
        OutboxVisible = !OutboxVisible;
        if (OutboxVisible)
        {
            await LoadOutboxAsync();
        }
    }

    protected override async Task OnNotifyFunction4()
    {
        ApiResult<SyncMastersResponse> result;
        using (var loading = dialog.Loading("同期しています..."))
        {
            result = await syncWorker.SyncMastersAsync(false, new Progress<string>(loading.Update), CancellationToken.None);
        }

        if (result.IsSuccess)
        {
            var sent = await syncWorker.SendOutboxAsync(CancellationToken.None);
            await dialog.Toast(sent > 0 ? $"同期しました。未送信 {sent} 件を送信しました。" : "同期しました。");
        }
        else
        {
            await dialog.InformationAsync("同期に失敗しました。\n" + result.Message);
        }

        await LoadOutboxAsync();
    }
}
