namespace Pos.Server.Components.Layout;

using Bunit;

using Pos.Server.Host.Components.Layout;

public sealed class NavMenuTests : MudBlazorTestBase
{
    [Fact]
    public void RenderShowsNavigationLinks()
    {
        // Arrange & Act
        var cut = Render<NavMenu>();

        // Assert
        var hrefs = cut.FindAll("a").Select(static x => x.GetAttribute("href")).ToList();
        Assert.Single(hrefs);
        Assert.Contains(string.Empty, hrefs);
    }
}
