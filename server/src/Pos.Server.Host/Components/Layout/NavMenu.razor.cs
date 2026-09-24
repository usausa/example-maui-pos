namespace Pos.Server.Host.Components.Layout;

using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Routing;

public sealed partial class NavMenu
{
    // 現在の URL を含むグループを開く
    private static readonly Dictionary<NavGroup, string[]> GroupRoutes = new()
    {
        [NavGroup.Reports] = ["reports"],
        [NavGroup.Transactions] = ["transactions", "orders"],
        [NavGroup.Closing] = ["shifts", "daily-closings"],
        [NavGroup.Inventory] = ["inventory"],
        [NavGroup.Products] = ["products", "categories", "tax-rates", "discounts", "payment-methods"],
        [NavGroup.Stores] = ["stores", "terminals", "staff"],
        [NavGroup.Settings] = ["settings"]
    };

    private readonly Dictionary<NavGroup, bool> expanded = Enum.GetValues<NavGroup>().ToDictionary(static x => x, static _ => false);

    [Inject]
    public required NavigationManager Navigation { get; set; }

    protected override void OnInitialized()
    {
        ApplyActiveGroup(Navigation.Uri);
        Navigation.LocationChanged += OnLocationChanged;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            Navigation.LocationChanged -= OnLocationChanged;
        }

        base.Dispose(disposing);
    }

    private bool IsExpanded(NavGroup group) => expanded[group];

    private void SetExpanded(NavGroup group, bool value) => expanded[group] = value;

    private void OnLocationChanged(object? sender, LocationChangedEventArgs e)
    {
        ApplyActiveGroup(e.Location);
        _ = InvokeAsync(StateHasChanged);
    }

    private void ApplyActiveGroup(string uri)
    {
        var group = ResolveGroup(new Uri(uri).AbsolutePath.TrimStart('/'));
        if (group.HasValue)
        {
            expanded[group.Value] = true;
        }
    }

    private static NavGroup? ResolveGroup(string path)
    {
        foreach (var (group, routes) in GroupRoutes)
        {
            if (routes.Any(route => path.Equals(route, StringComparison.OrdinalIgnoreCase) || path.StartsWith(route + "/", StringComparison.OrdinalIgnoreCase)))
            {
                return group;
            }
        }

        return null;
    }

    private enum NavGroup
    {
        Reports,
        Transactions,
        Closing,
        Inventory,
        Products,
        Stores,
        Settings
    }
}
