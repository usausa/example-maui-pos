namespace Pos.Server.Components.Layout;

using Bunit;

using Pos.Server.Host.Components.Layout;

public sealed class NavMenuTests : MudBlazorTestBase
{
    // 主要画面へのリンクがすべてある
    [Fact]
    public void RenderShowsNavigationLinks()
    {
        // Arrange & Act
        var cut = Render<NavMenu>();

        // Assert
        var hrefs = cut.FindAll("a").Select(static x => x.GetAttribute("href")).ToList();
        string[] expected =
        [
            string.Empty, "reports/sales", "reports/products", "transactions", "orders", "shifts", "daily-closings",
            "inventory", "inventory/changes", "inventory/receipts", "inventory/transfers", "inventory/reasons", "inventory/suppliers",
            "products", "categories", "tax-rates", "discounts", "payment-methods", "customers", "stores", "terminals", "staff", "settings"
        ];
        foreach (var href in expected)
        {
            Assert.Contains(href, hrefs);
        }
    }
}
