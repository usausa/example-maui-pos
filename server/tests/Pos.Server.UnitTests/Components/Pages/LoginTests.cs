namespace Pos.Server.Components.Pages;

using Bunit;

using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.DependencyInjection;

using Pos.Server.Host.Components.Pages;

public sealed class LoginTests : MudBlazorTestBase
{
    public LoginTests()
    {
        Services.AddSingleton<AntiforgeryStateProvider>(new FakeAntiforgeryStateProvider());
    }

    // 初めて開いたときはエラーを出さない
    [Fact]
    public void RenderShowsForm()
    {
        // Act
        var cut = Render<Login>();

        // Assert
        Assert.Empty(cut.FindAll(".mud-alert"));
        Assert.NotNull(cut.Find("input[name=name]"));
        Assert.NotNull(cut.Find("input[name=password][type=password]"));
    }

    // ログインに失敗して戻ってきたら理由を出し、戻り先を引き継ぐ
    [Fact]
    public void RenderShowsErrorAndKeepsReturnPath()
    {
        // Arrange
        Services.GetRequiredService<NavigationManager>().NavigateTo("login?error=1&returnUrl=products");

        // Act
        var cut = Render<Login>();

        // Assert
        Assert.Contains("ID またはパスワードが違います。", cut.Find(".mud-alert").TextContent, StringComparison.Ordinal);
        Assert.Equal("products", cut.Find("input[name=returnUrl]").GetAttribute("value"));
    }

    // 試行回数の上限にかかったとき
    [Fact]
    public void RenderShowsLimitError()
    {
        // Arrange
        Services.GetRequiredService<NavigationManager>().NavigateTo("login?error=limit");

        // Act
        var cut = Render<Login>();

        // Assert
        Assert.Contains("試行が多すぎます", cut.Find(".mud-alert").TextContent, StringComparison.Ordinal);
    }

    private sealed class FakeAntiforgeryStateProvider : AntiforgeryStateProvider
    {
        public override AntiforgeryRequestToken GetAntiforgeryToken() => new("token", "__RequestVerificationToken");
    }
}
