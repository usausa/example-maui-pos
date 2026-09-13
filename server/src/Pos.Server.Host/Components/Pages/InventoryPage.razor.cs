namespace Pos.Server.Host.Components.Pages;

using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

using MudBlazor;

using Pos.Server.Accessors;
using Pos.Server.Host.Components.Dialogs;
using Pos.Server.Host.Infrastructure.Api;
using Pos.Server.Host.Infrastructure.Components;
using Pos.Server.Host.Infrastructure.Data;
using Pos.Server.Host.Models.Forms;
using Pos.Server.Models;
using Pos.Server.Models.Entity;

using Smart.Data;

// S-40 現在庫 (行クリックで S-41 商品別全店在庫、[棚卸・調整] で S-43)
public sealed partial class InventoryPage
{
    private static readonly string[] SortColumns = ["ProductCode", "ProductName", "StoreName", "Quantity", "UpdatedAt"];

    private MudDataGrid<InventoryLevelDetail> Grid { get; set; } = default!;

    private List<CategoryEntity> categories = [];

    private Guid? storeId;

    private Guid? categoryId;

    private string? keyword;

    private bool negativeOnly;

    [Inject]
    public required InventoryAccessor InventoryAccessor { get; set; }

    [Inject]
    public required CategoryAccessor CategoryAccessor { get; set; }

    [Inject]
    public required IDbProvider Provider { get; set; }

    [Inject]
    public required StoreFilterState StoreFilter { get; set; }

    protected override Task OnInitializedAsync()
    {
        storeId = StoreFilter.StoreId;
        return LoadAsync(async () =>
        {
            categories = CategoryOrder.Sort(await CategoryAccessor.QueryListAsync(null, false, "SortOrder", ApiHelper.MaxPageSize, 0, CancellationToken));
        });
    }

    //--------------------------------------------------------------------------------
    // Grid
    //--------------------------------------------------------------------------------

    private async Task<GridData<InventoryLevelDetail>> LoadServerData(GridState<InventoryLevelDetail> state, CancellationToken cancellationToken)
    {
        var sort = state.SortDefinitions.FirstOrDefault();
        var order = SqlHelper.NormalizeSort(SortColumns, "ProductCode", sort?.SortBy, sort?.Descending ?? false);
        var pattern = ApiHelper.ToLikePattern(Dialect, keyword);
        var total = await InventoryAccessor.CountLevelDetailsAsync(storeId, categoryId, pattern, negativeOnly, cancellationToken);
        var items = await InventoryAccessor.QueryLevelDetailListAsync(storeId, categoryId, pattern, negativeOnly, order, state.PageSize, state.Page * state.PageSize, cancellationToken);
        return new GridData<InventoryLevelDetail> { TotalItems = (int)total, Items = items };
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
                { nameof(ProductInventoryDialog.ProductName), $"{args.Item.ProductCode} {args.Item.ProductName}" }
            },
            Styles.SmallDialog);
        await reference.Result;
    }

    //--------------------------------------------------------------------------------
    // Operation
    //--------------------------------------------------------------------------------

    // 棚卸・調整の登録 (API の POST /inventory/changes と同じ処理)
    private async Task RegisterChangeAsync()
    {
        var form = await ShowEditDialogAsync<InventoryChangeDialog, InventoryChangeForm>("棚卸・調整", new InventoryChangeForm { StoreId = storeId });
        if (form is null)
        {
            return;
        }

        await RunAsync(async () =>
        {
            var now = UtcNow;
            var change = await InventoryChangeApplier.ApplyAsync(
                InventoryAccessor,
                Provider,
                new InventoryChangeEntity
                {
                    Id = Guid.CreateVersion7(),
                    StoreId = form.StoreId!.Value,
                    ProductId = form.Product!.Id,
                    Type = form.Type,
                    ReasonId = form.ReasonId,
                    Reason = form.Reason,
                    StaffId = form.StaffId,
                    OccurredAt = now
                },
                form.Quantity,
                now,
                CancellationToken);
            Snackbar.AddSuccess($"登録しました。{form.Product.Name}: {DisplayText.Quantity(change.QuantityDelta)} → 在庫 {DisplayText.Quantity(change.QuantityAfter)}");
        }, SearchAsync);
    }
}
