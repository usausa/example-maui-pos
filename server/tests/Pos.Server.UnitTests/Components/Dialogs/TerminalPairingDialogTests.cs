namespace Pos.Server.Components.Dialogs;

using Bunit;

using Microsoft.Extensions.DependencyInjection;

using MudBlazor;

using Pos.Server.Host.Components.Dialogs;
using Pos.Server.Models.Entity;
using Pos.Server.Services;

public sealed class TerminalPairingDialogTests : MudBlazorTestBase
{
    // コードだけを出し、設定 QR は出さない (QR は印刷したものを読む)
    [Fact]
    public async Task ShowsCodeWithoutQr()
    {
        // Act
        var provider = Render<MudDialogProvider>();
        var parameters = new DialogParameters<TerminalPairingDialog>
        {
            { static x => x.Terminal, new TerminalEntity { Name = "本店 レジ 1" } },
            { static x => x.StoreName, "本店" },
            { static x => x.Code, new TerminalPairingCode("123456", new DateTime(2026, 1, 1, 0, 10, 0, DateTimeKind.Utc)) }
        };
        await provider.InvokeAsync(() => Services.GetRequiredService<IDialogService>().ShowAsync<TerminalPairingDialog>(string.Empty, parameters));

        // Assert
        Assert.Equal("123456", provider.Find(".pairing-code").TextContent);
        Assert.Empty(provider.FindAll("img"));
    }
}
