namespace Pos.Server.Components.Layout;

using Bunit;

using Pos.Server.Host.Application.Authentication;
using Pos.Server.Host.Components.Layout;

public sealed class NavMenuTests : MudBlazorTestBase
{
    // 主要画面へのリンクがすべてあり、管理者にはユーザーの画面も出る
    [Fact]
    public void RenderShowsNavigationLinks()
    {
        // Arrange
        AddAuthorization().SetAuthorized("admin").SetPolicies(Policies.Administrator);

        // Act
        var cut = Render<NavMenu>();

        // Assert
        var hrefs = cut.FindAll("a").Select(static x => x.GetAttribute("href")).ToList();
        string[] expected =
        [
            string.Empty, "reports/sales", "reports/products", "transactions", "orders", "shifts", "daily-closings",
            "inventory", "inventory/changes", "inventory/receipts", "inventory/transfers", "inventory/reasons", "inventory/suppliers",
            "products", "categories", "tax-rates", "discounts", "payment-methods", "customers", "stores", "terminals", "staff", "settings", "accounts"
        ];
        foreach (var href in expected)
        {
            Assert.Contains(href, hrefs);
        }
    }

    // オペレーターにはユーザーの画面を出さない
    [Fact]
    public void RenderHidesAccountsForOperator()
    {
        // Arrange
        AddAuthorization().SetAuthorized("operator");

        // Act
        var cut = Render<NavMenu>();

        // Assert
        var hrefs = cut.FindAll("a").Select(static x => x.GetAttribute("href")).ToList();
        Assert.Contains("settings", hrefs);
        Assert.DoesNotContain("accounts", hrefs);
    }
}
