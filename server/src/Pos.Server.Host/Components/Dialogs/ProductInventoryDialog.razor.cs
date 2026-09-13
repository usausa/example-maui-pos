namespace Pos.Server.Host.Components.Dialogs;

using Microsoft.AspNetCore.Components;

using MudBlazor;

using Pos.Server.Accessors;
using Pos.Server.Models;

// S-41 商品別全店在庫
public sealed partial class ProductInventoryDialog
{
    private List<ProductInventoryLevel> levels = [];

    [Parameter]
    public Guid ProductId { get; set; }

    [Parameter]
    public required string ProductName { get; set; }

    [CascadingParameter]
    public required IMudDialogInstance MudDialog { get; set; }

    [Inject]
    public required InventoryAccessor InventoryAccessor { get; set; }

    protected override async Task OnInitializedAsync()
    {
        levels = await InventoryAccessor.QueryLevelsByProductAsync(ProductId, CancellationToken.None);
    }
}
