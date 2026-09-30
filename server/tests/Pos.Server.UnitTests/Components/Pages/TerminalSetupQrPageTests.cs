namespace Pos.Server.Components.Pages;

using Bunit;

using Microsoft.Extensions.DependencyInjection;

using Pos.Server.Host.Components.Pages;
using Pos.Server.Host.Settings;

public sealed class TerminalSetupQrPageTests : MudBlazorTestBase
{
    // 設定がなければ管理画面を開いた URL を接続先にし、localhost なら端末から届かないと知らせる
    [Fact]
    public void LoopbackEndPointShowsWarning()
    {
        // Arrange
        Services.AddSingleton(new TerminalSetting());

        // Act
        var cut = Render<TerminalSetupQrPage>();

        // Assert
        Assert.Equal("http://localhost/", cut.Find(".qr-text").TextContent);
        Assert.Single(cut.FindAll(".mud-alert"));
    }

    // 設定があれば、その URL を接続先にする
    [Fact]
    public void ConfiguredEndPointIsUsed()
    {
        // Arrange
        Services.AddSingleton(new TerminalSetting { ApiEndPoint = "http://192.168.0.10:8080/" });

        // Act
        var cut = Render<TerminalSetupQrPage>();

        // Assert
        Assert.Equal("http://192.168.0.10:8080/", cut.Find(".qr-text").TextContent);
        Assert.Empty(cut.FindAll(".mud-alert"));
    }
}
