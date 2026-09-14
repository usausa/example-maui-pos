namespace Pos.Server.Host.Components.Pages;

using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

using MudBlazor;

using Pos.Server.Host.Components.Dialogs;
using Pos.Server.Host.Models.Forms;
using Pos.Server.Models.Entity;
using Pos.Server.Models.Parameters;
using Pos.Server.Models.Views;
using Pos.Server.Services;

// S-40 現在庫 (行クリックで S-41 商品別全店在庫、[棚卸・調整] で S-43)
public sealed partial class InventoryPage
{
    private MudDataGrid<InventoryLevelDetail> Grid { get; set; } = default!;

    private List<CategoryEntity> categories = [];
    private Guid? storeId;
    private Guid? categoryId;
    private string? keyword;
    private bool negativeOnly;

    [Inject]
    public required InventoryService InventoryService { get; set; }

    [Inject]
    public required CategoryService CategoryService { get; set; }

    [Inject]
    public required StoreFilterState StoreFilter { get; set; }

    protected override Task OnInitializedAsync()
    {
        storeId = StoreFilter.StoreId;
        return LoadAsync(async () =>
        {
            categories = await CategoryService.QueryAllAsync(false, CancellationToken);
        });
    }

    //--------------------------------------------------------------------------------
    // Grid
    //--------------------------------------------------------------------------------

    private async Task<GridData<InventoryLevelDetail>> LoadServerData(GridState<InventoryLevelDetail> state, CancellationToken cancellationToken)
    {
        var sort = state.SortDefinitions.FirstOrDefault();
        var parameter = new InventoryLevelDetailQueryParameter
        {
            StoreId = storeId,
            CategoryId = categoryId,
            Keyword = keyword,
            NegativeOnly = negativeOnly,
            Sort = sort?.SortBy,
            Desc = sort?.Descending ?? false,
            Page = state.Page,
            Size = state.PageSize
        };
        var result = await InventoryService.QueryLevelDetailPageAsync(parameter, cancellationToken);
        return new GridData<InventoryLevelDetail> { TotalItems = result.Total, Items = result.Items };
    }

    private Task SearchAsync() => Grid.ReloadServerData();

    private Task OnSearchKeyDown(KeyboardEventArgs args) =>
        args.Key == "Enter" ? SearchAsync() : Task.CompletedTask;

    private Task OnStoreChanged(Guid? value)
    {
        storeId = value;
        StoreFilter.StoreId = value;
        return SearchAsync();
    }

    private async Task OnRowClick(DataGridRowClickEventArgs<InventoryLevelDetail> args)
    {
        var reference = await DialogService.ShowAsync<ProductInventoryDialog>(
            string.Empty,
            new DialogParameters
            {
                { nameof(ProductInventoryDialog.ProductId), args.Item.ProductId },
                { nameof(ProductInventoryDialog.ProductName), args.Item.ToProductText() }
            },
            Styles.SmallDialog);
        await reference.Result;
    }

    //--------------------------------------------------------------------------------
    // Operation
    //--------------------------------------------------------------------------------

    // 棚卸・調整の登録
    private async Task RegisterChangeAsync()
    {
        var form = await ShowEditDialogAsync<InventoryChangeDialog, InventoryChangeForm>("棚卸・調整", new InventoryChangeForm { StoreId = storeId });
        if (form is null)
        {
            return;
        }

        await RunAsync(async () =>
        {
            var parameter = InventoryChangeForm.ToParameter(form);
            parameter.Id = Guid.CreateVersion7();
            parameter.OccurredAt = UtcNow;
            var change = await InventoryService.ApplyChangeAsync(parameter, CancellationToken);
            Snackbar.AddSuccess($"登録しました。{form.Product!.Name}: {change.QuantityDelta.ToQuantityText()} → 在庫 {change.QuantityAfter.ToQuantityText()}");
        }, SearchAsync);
    }
}
