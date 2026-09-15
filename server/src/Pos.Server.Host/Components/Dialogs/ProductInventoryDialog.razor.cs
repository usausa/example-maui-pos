namespace Pos.Server.Host.Components.Dialogs;

using Microsoft.AspNetCore.Components;

using MudBlazor;

using Pos.Server.Models.Views;
using Pos.Server.Services;

// 商品別全店在庫
public sealed partial class ProductInventoryDialog
{
    private IReadOnlyList<ProductInventoryLevelView> levels = [];

    [Parameter]
    public Guid ProductId { get; set; }

    [Parameter]
    public required string ProductName { get; set; }

    [CascadingParameter]
    public required IMudDialogInstance MudDialog { get; set; }

    [Inject]
    public required InventoryService InventoryService { get; set; }

    protected override async Task OnInitializedAsync()
    {
        levels = await InventoryService.QueryProductLevelsAsync(ProductId, CancellationToken.None) ?? [];
    }
}
