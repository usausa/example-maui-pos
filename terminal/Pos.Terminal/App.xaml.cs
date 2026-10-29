namespace Pos.Terminal;

using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;

using Pos.Terminal.Diagnostics;
using Pos.Terminal.Services;

#pragma warning disable CA1724
public sealed partial class App
{
    private readonly IServiceProvider serviceProvider;

    private readonly ILogger<App> log;

    public App(IServiceProvider serviceProvider, ILogger<App> log)
    {
        this.serviceProvider = serviceProvider;
        this.log = log;

        Current!.UserAppTheme = AppTheme.Light;

        InitializeComponent();
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        return new Window(serviceProvider.GetRequiredService<MainPage>());
    }

    // ReSharper disable once AsyncVoidMethod
    protected override async void OnStart()
    {
        await CrashReport.ShowReport();

        var initializeError = await InitializeDataAsync();
        if (initializeError is not null)
        {
            var page = Current?.Windows[0].Page;
            if (page is not null)
            {
                await page.DisplayAlertAsync("Initialize error", $"Failed to initialize database.\r\n{initializeError.Message}", "Exit");
            }

            Current?.Quit();
            return;
        }

        log.InfoApplicationStart(typeof(App).Assembly.GetName().Version, Environment.Version);

        serviceProvider.GetRequiredService<SyncService>().Start();

        serviceProvider.GetRequiredService<StartupState>().NotifyCompleted();
    }

    private async Task<Exception?> InitializeDataAsync()
    {
        try
        {
            await serviceProvider.GetRequiredService<DatabaseService>().InitializeAsync();
            await serviceProvider.GetRequiredService<CredentialService>().LoadAsync();
            await serviceProvider.GetRequiredService<SyncService>().RefreshSessionAsync();

            return null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or SqliteException)
        {
            log.ErrorDatabaseInitializeFailed(ex);
            return ex;
        }
    }
}
#pragma warning restore CA1724
