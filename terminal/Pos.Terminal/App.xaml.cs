namespace Pos.Terminal;

using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;

#pragma warning disable CA1724
public sealed partial class App
{
    private readonly ILogger<App> log;

    private readonly IServiceProvider serviceProvider;

    public App(ILogger<App> log, IServiceProvider serviceProvider)
    {
        this.log = log;
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

        // ローカル DB を開けないときは理由を出して終了する (未送信を含む DB を作り直さない)。
        // 画面の仕組みが揃う前なので OS のダイアログで出す
        if (await InitializeDataAsync() is { } error)
        {
            var page = Current?.Windows[0].Page;
            if (page is not null)
            {
                await page.DisplayAlertAsync("起動できません", $"データベースを開けませんでした。\n{error.Message}", "終了");
            }

            Current?.Quit();
            return;
        }

        serviceProvider.GetRequiredService<SyncService>().Start();

        // Completed
        serviceProvider.GetRequiredService<StartupState>().NotifyCompleted();
    }

    // ローカル DB と端末の登録 (トークン) とセッション
    private async ValueTask<Exception?> InitializeDataAsync()
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
