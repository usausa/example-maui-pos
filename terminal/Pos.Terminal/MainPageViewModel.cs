namespace Pos.Terminal;

using CommunityToolkit.Maui.Core;

using Pos.Terminal.Modules;
using Pos.Terminal.Shell;

// 画面の枠: 表題、店舗・端末と担当、未送信の数、F キーを出し、登録が解除されたら初期設定へ戻す
[ObservableGeneratorOption(Reactive = true, ViewModel = true)]
public sealed partial class MainPageViewModel : ExtendViewModelBase, IShellControl, IAppLifecycle
{
    private readonly IScreen screen;

    private readonly IDialog dialog;

    private readonly StartupState startup;

    private readonly Session session;

    private readonly CredentialService credential;

    private readonly SyncService syncService;

    private bool destroying;

    private IDisposable? navigatingBusy;

    public INavigator Navigator { get; }

    public NotificationValue<string> Title { get; } = new(string.Empty);

    public NotificationValue<bool> HeaderVisible { get; } = new();

    public NotificationValue<bool> FunctionVisible { get; } = new();

    public NotificationValue<Color?> StatusBarColor { get; } = new();

    public NotificationValue<StatusBarStyle> StatusBarStyle { get; } = new(CommunityToolkit.Maui.Core.StatusBarStyle.Default);

    public IReadOnlyList<FunctionState> Functions { get; } = [new(), new(), new(), new()];

    public FunctionState Function1 => Functions[0];
    public FunctionState Function2 => Functions[1];
    public FunctionState Function3 => Functions[2];
    public FunctionState Function4 => Functions[3];

    // タイトルバー右側: 店舗-端末 担当 と未送信バッジ
    [ObservableProperty]
    public partial string HeaderText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial int UnsentCount { get; set; }

    [ObservableProperty]
    public partial bool BadgeVisible { get; set; }

    // 要確認があれば赤 (色は画面側の Converter で付ける)
    [ObservableProperty]
    public partial bool HasFailed { get; set; }

    public IObserveCommand Function1Command { get; }
    public IObserveCommand Function2Command { get; }
    public IObserveCommand Function3Command { get; }
    public IObserveCommand Function4Command { get; }

    //--------------------------------------------------------------------------------
    // Constructor
    //--------------------------------------------------------------------------------

    public MainPageViewModel(
        ILogger<MainPageViewModel> log,
        INavigator navigator,
        IScreen screen,
        IDialog dialog,
        StartupState startup,
        Session session,
        ApiContext apiContext,
        CredentialService credential,
        SyncService syncService)
    {
        Navigator = navigator;
        this.screen = screen;
        this.dialog = dialog;
        this.startup = startup;
        this.session = session;
        this.credential = credential;
        this.syncService = syncService;

        Function1Command = CreateFunctionCommand(Functions[0], ShellEvent.Function1);
        Function2Command = CreateFunctionCommand(Functions[1], ShellEvent.Function2);
        Function3Command = CreateFunctionCommand(Functions[2], ShellEvent.Function3);
        Function4Command = CreateFunctionCommand(Functions[3], ShellEvent.Function4);

        // 遷移の間は Busy にして、ボタン・F キー・戻るを受け付けない
        Disposables.Add(Observable.FromEventPattern<EventArgs>(h => Navigator.ExecutingChanged += h, h => Navigator.ExecutingChanged -= h)
            .Subscribe(_ => UpdateNavigatingBusy()));

        Disposables.Add(session.PropertyChangedAsObservable().ObserveOnCurrentContext().Subscribe(_ => UpdateHeader()));
        UpdateHeader();

        // サーバで端末の登録が解除された (要求が 401): 知らせて初期設定へ
        Disposables.Add(Observable.FromEventPattern(h => apiContext.Unauthorized += h, h => apiContext.Unauthorized -= h)
            .ObserveOnCurrentContext()
            .Subscribe(_ => HandleUnauthorized()));

        // 画面が点いたら同期を促す
        Disposables.Add(screen.StateChangedAsObservable().ObserveOnCurrentContext().Subscribe(x =>
        {
            log.DebugScreenStateChanged(x.ScreenOn);
            if (x.ScreenOn)
            {
                syncService.Trigger();
            }
        }));
    }

    private IObserveCommand CreateFunctionCommand(FunctionState function, ShellEvent shellEvent)
    {
        var command = MakeAsyncCommand(() => Navigator.NotifyAsync(shellEvent), () => function.Enabled.Value);
        return Observe(function.Enabled.AsObservable(nameof(NotificationValue<>.Value)), command);
    }

    // ReSharper disable once AsyncVoidMethod
    private async void HandleUnauthorized()
    {
        credential.Clear();
        session.Staff = null;
        await dialog.InformationAsync("登録が解除されました", "端末の登録");
        await Navigator.ForwardAsync(ViewId.Setup);
    }

    private void UpdateHeader()
    {
        HeaderText = session.HeaderText;
        UnsentCount = session.UnsentCount;
        BadgeVisible = session.UnsentCount > 0;
        HasFailed = session.FailedCount > 0;
    }

    //--------------------------------------------------------------------------------
    // Lifecycle
    //--------------------------------------------------------------------------------

    // ReSharper disable once AsyncVoidMethod
    public async void OnCreated()
    {
        screen.EnableDetectScreenState(true);

        await startup.Completed;

        // 初期化の途中で Activity が作り直されたときは、ここで止める
        if (destroying)
        {
            return;
        }

        Navigator.Exit();
        await Navigator.ForwardAsync(credential.IsRegistered ? ViewId.StaffSelect : ViewId.Setup);
    }

    public void OnActivated()
    {
    }

    public void OnDeactivated()
    {
    }

    public void OnStopped()
    {
    }

    public void OnResumed()
    {
        syncService.Trigger();
    }

    public void OnDestroying()
    {
        destroying = true;
    }

    //--------------------------------------------------------------------------------
    // Navigation
    //--------------------------------------------------------------------------------

    private void UpdateNavigatingBusy()
    {
        if (Navigator.Executing)
        {
            navigatingBusy ??= BusyState.Begin();
        }
        else
        {
            navigatingBusy?.Dispose();
            navigatingBusy = null;
        }
    }
}
