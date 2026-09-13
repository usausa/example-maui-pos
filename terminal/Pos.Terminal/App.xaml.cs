namespace Pos.Terminal;

using Microsoft.Extensions.DependencyInjection;

using Smart.Data;

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

        // ローカル DB とセッション
        var provider = serviceProvider.GetRequiredService<IDbProvider>();
        var accessor = serviceProvider.GetRequiredService<DataAccessor>();
        await provider.UsingAsync(async con =>
        {
            await accessor.ExecutePragmaAsync(con);
            await accessor.CreateTablesAsync(con);
        });

        var syncWorker = serviceProvider.GetRequiredService<SyncWorker>();
        await syncWorker.RefreshSessionAsync();
        syncWorker.Start();

        // Completed
        serviceProvider.GetRequiredService<StartupState>().NotifyCompleted();
    }
}
#pragma warning restore CA1724
