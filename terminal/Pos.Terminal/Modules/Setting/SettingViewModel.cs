namespace Pos.Terminal.Modules.Setting;

using Pos.Terminal.Models.Entity;

// 未送信の 1 件。状態・種類の文言と色は画面側の Converter で付け、エラーの全文と試行回数は展開したときに見せる
public sealed class OutboxItem : NotificationObject
{
    public required OutboxEntity Entity { get; init; }

    public required OutboxStatus Status { get; init; }

    public required OutboxKind Kind { get; init; }

    public required string TimeText { get; init; }

    public required int Attempts { get; init; }

    public required string Error { get; init; }

    public bool IsExpanded
    {
        get;
        set => SetProperty(ref field, value);
    }
}

// 設定・同期: 端末情報と登録、手動同期、未送信一覧 (要確認の再送・破棄)、ログイン後の画面、スタッフ切替、登録の解除
public sealed partial class SettingViewModel : AppViewModelBase
{
    private readonly IDialog dialog;

    private readonly IPopupNavigator popupNavigator;

    private readonly IAppInfo appInfo;

    private readonly Settings settings;

    private readonly Session session;

    private readonly DataAccessor accessor;

    private readonly CredentialService credential;

    private readonly SyncService sync;

    private ViewId returnTo = ViewId.Menu;

    public ObservableCollection<SummaryRow> Info { get; } = [];

    [ObservableProperty]
    public partial string LastSyncText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string UnsentText { get; set; } = string.Empty;

    // 未送信なし = Sent、未送信あり = Pending、要確認あり = Failed
    [ObservableProperty]
    public partial OutboxStatus SyncStatus { get; set; } = OutboxStatus.Sent;

    [ObservableProperty]
    public partial bool OutboxVisible { get; set; }

    public ObservableCollection<OutboxItem> Outbox { get; } = [];

    [ObservableProperty]
    public partial bool OpenSalesAfterLogin { get; set; }

    public IObserveCommand OutboxCommand { get; }

    public IObserveCommand ExpandCommand { get; }

    public IObserveCommand SwitchStaffCommand { get; }

    public IObserveCommand UnregisterCommand { get; }

    public SettingViewModel(
        IDialog dialog,
        IPopupNavigator popupNavigator,
        IAppInfo appInfo,
        Settings settings,
        Session session,
        DataAccessor accessor,
        CredentialService credential,
        SyncService sync)
    {
        this.dialog = dialog;
        this.popupNavigator = popupNavigator;
        this.appInfo = appInfo;
        this.settings = settings;
        this.session = session;
        this.accessor = accessor;
        this.credential = credential;
        this.sync = sync;

        OutboxCommand = MakeAsyncCommand<OutboxItem>(HandleOutboxAsync);
        ExpandCommand = MakeDelegateCommand<OutboxItem>(static x => x.IsExpanded = !x.IsExpanded);
        SwitchStaffCommand = MakeAsyncCommand(() => Navigator.ForwardAsync(ViewId.StaffSelect));
        UnregisterCommand = MakeAsyncCommand(UnregisterAsync);

        Disposables.Add(session.PropertyChangedAsObservable().ObserveOnCurrentContext().Subscribe(_ => UpdateSync()));
        SubscribeOpenSalesAfterLogin(x => settings.OpenSalesAfterLogin = x);
    }

    public override async Task OnNavigatedToAsync(INavigationContext context)
    {
        returnTo = context.Parameter.GetReturnTo(ViewId.Menu);
        OpenSalesAfterLogin = settings.OpenSalesAfterLogin;
        Info.Replace(
        [
            new SummaryRow("店舗", session.Store is null ? "-" : $"{session.Store.Name} ({session.Store.Code})"),
            new SummaryRow("端末", session.Terminal is null ? "-" : $"{session.Terminal.Name} (No.{session.Terminal.TerminalNo})"),
            new SummaryRow("担当", session.Staff?.Name ?? "-"),
            new SummaryRow("サーバ", settings.ApiEndPoint),
            new SummaryRow("登録", settings.PairedAt is { } pairedAt ? ViewHelper.DateTime(pairedAt) : "-"),
            new SummaryRow("バージョン", appInfo.VersionString)
        ]);
        UpdateSync();
        await Navigator.PostActionAsync(LoadOutboxAsync);
    }

    private void UpdateSync()
    {
        LastSyncText = session.LastSyncAt is null ? "最終同期: -" : $"最終同期: {ViewHelper.DateTime(session.LastSyncAt.Value)}";
        UnsentText = session.UnsentCount == 0 ? "未送信なし" : session.FailedCount > 0 ? $"未送信 {session.UnsentCount} / 要確認 {session.FailedCount}" : $"未送信 {session.UnsentCount}";
        SyncStatus = session.FailedCount > 0 ? OutboxStatus.Failed : session.UnsentCount > 0 ? OutboxStatus.Pending : OutboxStatus.Sent;
    }

    private async Task LoadOutboxAsync()
    {
        Outbox.Replace((await accessor.QueryOutboxListAsync(null, 200)).Select(static x => new OutboxItem
        {
            Entity = x,
            Status = x.Status,
            Kind = x.Kind,
            TimeText = ViewHelper.DateTime(x.CreatedAt),
            Attempts = x.Attempts,
            Error = x.LastError ?? string.Empty
        }));
    }

    private async Task HandleOutboxAsync(OutboxItem item)
    {
        var entity = item.Entity;
        var kind = ViewHelper.Name(item.Kind);
        if (entity.Status != OutboxStatus.Failed)
        {
            await dialog.InformationAsync($"{kind}\n{item.TimeText}\n試行 {entity.Attempts} 回\n{entity.LastError}", "未送信");
            return;
        }

        var index = await popupNavigator.ChooseAsync(["🔁 再送する", "🗑️ 破棄する (サーバには送らない)", "ℹ️ 詳細"], kind);
        switch (index)
        {
            case 0:
                await sync.RetryAsync(entity.Id);
                await dialog.Toast("再送します");
                break;
            case 1:
                if (await dialog.AskAsync("この送信を破棄しますか？\nサーバには反映されません", "破棄", "破棄"))
                {
                    await sync.DiscardAsync(entity.Id);
                }

                break;
            case 2:
                await dialog.InformationAsync($"{kind}\n{item.TimeText}\n試行 {entity.Attempts} 回\n{entity.LastError}\n\n{entity.Payload}", "要確認");
                break;
        }

        await LoadOutboxAsync();
    }

    // 端末のトークンを捨てて初期設定へ。ローカル DB と未送信は残し、店舗コード・端末番号が同じ端末に登録し直すと続きを送る
    private async Task UnregisterAsync()
    {
        if (!await dialog.AskAsync("登録を解除しますか？\n未送信は同じ店舗・番号で登録し直すと送信", "登録の解除", "解除"))
        {
            return;
        }

        credential.Clear();
        session.Staff = null;
        await Navigator.ForwardAsync(ViewId.Setup);
    }

    protected override Task OnNotifyBackAsync() => Navigator.ForwardAsync(returnTo);

    protected override Task OnNotifyFunction1() => OnNotifyBackAsync();

    // 登録し直しは初期設定のカメラで設定 QR を読む。初期設定から戻ったときのために、この画面の戻り先を引き継ぐ
    protected override Task OnNotifyFunction2() =>
        Navigator.ForwardAsync(ViewId.Setup, Parameters.Make().WithCallerReturnTo(returnTo));

    protected override Task OnNotifyFunction3()
    {
        OutboxVisible = !OutboxVisible;
        return OutboxVisible ? LoadOutboxAsync() : Task.CompletedTask;
    }

    protected override async Task OnNotifyFunction4()
    {
        (ApiResult<Pos.Contract.Sync.SyncMastersResponse> Result, int Sent) result;
        using (dialog.Indicator())
        {
            result = await sync.SyncAllAsync(CancellationToken.None);
        }

        if (result.Result.IsSuccess)
        {
            await dialog.Toast(result.Sent > 0 ? $"同期しました (送信 {result.Sent} 件)" : "同期しました");
        }
        else
        {
            await dialog.InformationAsync("同期できませんでした\n" + result.Result.Message);
        }

        await LoadOutboxAsync();
    }
}
