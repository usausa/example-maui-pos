namespace Pos.Server.Components.Dialogs;

using Bunit;

using Microsoft.Extensions.DependencyInjection;

using MudBlazor;

using Pos.Server.Host.Components.Dialogs;
using Pos.Server.Host.Settings;
using Pos.Server.Models.Entity;
using Pos.Server.Services;

public sealed class TerminalPairingDialogTests : MudBlazorTestBase
{
    // 設定がなければ管理画面を開いた URL を設定 QR の接続先にし、localhost なら端末から届かないと知らせる
    [Fact]
    public async Task LoopbackEndPointShowsWarning()
    {
        // Arrange
        Services.AddSingleton(new TerminalSetting());

        // Act
        var provider = await ShowAsync();

        // Assert
        Assert.Contains("ApiEndPoint=http://localhost/", provider.Markup, StringComparison.Ordinal);
        Assert.Contains("PairingCode=123456", provider.Markup, StringComparison.Ordinal);
        Assert.Single(provider.FindAll(".mud-alert"));
    }

    // 設定があれば、その URL を設定 QR の接続先にする
    [Fact]
    public async Task ConfiguredEndPointIsUsed()
    {
        // Arrange
        Services.AddSingleton(new TerminalSetting { ApiEndPoint = "http://192.168.0.10:8080/" });

        // Act
        var provider = await ShowAsync();

        // Assert
        Assert.Contains("ApiEndPoint=http://192.168.0.10:8080/", provider.Markup, StringComparison.Ordinal);
        Assert.Empty(provider.FindAll(".mud-alert"));
    }

    private async Task<IRenderedComponent<MudDialogProvider>> ShowAsync()
    {
        var provider = Render<MudDialogProvider>();
        var parameters = new DialogParameters<TerminalPairingDialog>
        {
            { static x => x.Terminal, new TerminalEntity { Name = "本店 レジ 1" } },
            { static x => x.StoreName, "本店" },
            { static x => x.Code, new TerminalPairingCode("123456", new DateTime(2026, 1, 1, 0, 10, 0, DateTimeKind.Utc)) }
        };
        await provider.InvokeAsync(() => Services.GetRequiredService<IDialogService>().ShowAsync<TerminalPairingDialog>(string.Empty, parameters));
        return provider;
    }
}
