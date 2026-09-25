namespace Pos.Server;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

public class TestApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string databaseFile = $"test-{Guid.NewGuid():N}.db";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("http_ports", string.Empty);
        builder.UseSetting("ConnectionStrings:Default", $"Data Source={databaseFile};Cache=Shared;Pooling=False;Foreign Keys=True");
        builder.UseSetting("Prometheus:Uri", string.Empty);
        builder.UseSetting("Profiler:SqlLog:Enable", "false");
        builder.UseSetting("Profiler:SqlTelemetry:Enable", "false");
        builder.UseSetting("Log:HttpLog", "false");
        // テストごとにログインとペアリングを行うので、試行回数の上限にかからないようにする
        builder.UseSetting("Auth:AttemptsPerMinute", "100000");
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (disposing && File.Exists(databaseFile))
        {
            try
            {
                File.Delete(databaseFile);
            }
            catch (IOException)
            {
                // Ignore
            }
        }
    }
}

// 認証を無効にしたサーバ (開発・デモ)
public sealed class AuthDisabledApplicationFactory : TestApplicationFactory
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.UseSetting("Auth:Enabled", "false");
    }
}
