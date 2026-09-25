namespace Pos.Terminal;

using Microsoft.Extensions.DependencyInjection;

#pragma warning disable CA1724
public sealed partial class App
{
    private readonly IServiceProvider serviceProvider;

    public App(IServiceProvider serviceProvider, ILogger<App> log)
    {
        this.serviceProvider = serviceProvider;

        // Light theme based application
        Current!.UserAppTheme = AppTheme.Light;

        InitializeComponent();

        // Start
        log.InfoApplicationStart(typeof(App).Assembly.GetName().Version, Environment.Version);
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        return new Window(serviceProvider.GetRequiredService<MainPage>());
    }

    // ReSharper disable once AsyncVoidMethod
    protected override async void OnStart()
    {
        // Report previous exception
        await CrashReport.ShowReport();

        // ローカル DB と端末の登録 (トークン) とセッション
        await serviceProvider.GetRequiredService<DatabaseService>().InitializeAsync();
        await serviceProvider.GetRequiredService<CredentialService>().LoadAsync();

        var syncService = serviceProvider.GetRequiredService<SyncService>();
        await syncService.RefreshSessionAsync();
        syncService.Start();

        // Completed
        serviceProvider.GetRequiredService<StartupState>().NotifyCompleted();
    }
}
#pragma warning restore CA1724
