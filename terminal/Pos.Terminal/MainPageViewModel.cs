namespace Pos.Terminal;

using Pos.Terminal.Modules;
using Pos.Terminal.Shell;

[ObservableGeneratorOption(Reactive = true, ViewModel = true)]
public sealed partial class MainPageViewModel : ExtendViewModelBase, IShellControl, IAppLifecycle
{
    private static readonly Color UnsentColor = Color.FromArgb("#FB8C00");

    private static readonly Color FailedColor = Color.FromArgb("#E53935");

    private readonly IScreen screen;

    private readonly StartupState startup;

    private readonly Settings settings;

    private readonly Session session;

    private readonly SyncWorker syncWorker;

    private bool destroying;

    public INavigator Navigator { get; }

    [ObservableProperty]
    public partial string Title { get; set; } = default!;

    [ObservableProperty]
    public partial bool HeaderVisible { get; set; }

    [ObservableProperty]
    public partial bool FunctionVisible { get; set; }

    [ObservableProperty]
    public partial string Function1Text { get; set; } = default!;
    [ObservableProperty]
    public partial string Function2Text { get; set; } = default!;
    [ObservableProperty]
    public partial string Function3Text { get; set; } = default!;
    [ObservableProperty]
    public partial string Function4Text { get; set; } = default!;

    [ObservableProperty]
    public partial bool Function1Enabled { get; set; }
    [ObservableProperty]
    public partial bool Function2Enabled { get; set; }
    [ObservableProperty]
    public partial bool Function3Enabled { get; set; }
    [ObservableProperty]
    public partial bool Function4Enabled { get; set; }

    // タイトルバー右側: 店舗-端末 担当 と未送信バッジ (screen-design §1.5)
    [ObservableProperty]
    public partial string HeaderText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial int UnsentCount { get; set; }

    [ObservableProperty]
    public partial bool BadgeVisible { get; set; }

    [ObservableProperty]
    public partial Color BadgeColor { get; set; } = UnsentColor;

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
        StartupState startup,
        Settings settings,
        Session session,
        SyncWorker syncWorker)
    {
        Navigator = navigator;
        this.screen = screen;
        this.startup = startup;
        this.settings = settings;
        this.session = session;
        this.syncWorker = syncWorker;

        Function1Command = MakeAsyncCommand(() => Navigator.NotifyAsync(ShellEvent.Function1), () => Function1Enabled);
        Function2Command = MakeAsyncCommand(() => Navigator.NotifyAsync(ShellEvent.Function2), () => Function2Enabled);
        Function3Command = MakeAsyncCommand(() => Navigator.NotifyAsync(ShellEvent.Function3), () => Function3Enabled);
        Function4Command = MakeAsyncCommand(() => Navigator.NotifyAsync(ShellEvent.Function4), () => Function4Enabled);

        Disposables.Add(session.PropertyChangedAsObservable().ObserveOnCurrentContext().Subscribe(_ => UpdateHeader()));
        UpdateHeader();

        // Screen lock detection
        Disposables.Add(screen.StateChangedAsObservable().ObserveOnCurrentContext().Subscribe(x =>
        {
            log.DebugScreenStateChanged(x.ScreenOn);
            if (x.ScreenOn)
            {
                syncWorker.Trigger();
            }
        }));
    }

    private void UpdateHeader()
    {
        HeaderText = session.HeaderText;
        UnsentCount = session.UnsentCount;
        BadgeVisible = session.UnsentCount > 0;
        BadgeColor = session.FailedCount > 0 ? FailedColor : UnsentColor;
    }

    //--------------------------------------------------------------------------------
    // Lifecycle
    //--------------------------------------------------------------------------------

    // ReSharper disable once AsyncVoidMethod
    public async void OnCreated()
    {
        screen.EnableDetectScreenState(true);

        await startup.Completed;

        // Guard for the case where the Activity is recreated while initialization is still in progress
        if (destroying)
        {
            return;
        }

        Navigator.Exit();
        await Navigator.ForwardAsync(settings.IsConfigured ? ViewId.StaffSelect : ViewId.Setup);
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
        syncWorker.Trigger();
    }

    public void OnDestroying()
    {
        destroying = true;
    }
}
