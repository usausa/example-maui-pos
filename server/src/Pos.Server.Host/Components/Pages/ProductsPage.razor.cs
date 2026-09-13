namespace Pos.Server.Host.Components.Pages;

using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

using MudBlazor;

using Pos.Server.Accessors;
using Pos.Server.Host.Components.Dialogs;
using Pos.Server.Host.Infrastructure.Api;
using Pos.Server.Host.Infrastructure.Components;
using Pos.Server.Host.Infrastructure.Data;
using Pos.Server.Host.Mappers;
using Pos.Server.Host.Models.Forms;
using Pos.Server.Models.Entity;

// S-50 商品一覧 / S-51 商品編集
public sealed partial class ProductsPage
{
    private static readonly string[] SortColumns = ["Code", "Name", "Price", "UpdatedAt"];

    private MudDataGrid<ProductEntity> Grid { get; set; } = default!;

    private List<CategoryEntity> categories = [];

    private Dictionary<Guid, CategoryEntity> categoryMap = [];

    private Dictionary<Guid, TaxRateEntity> taxRates = [];

    private Guid? categoryId;

    private string? keyword;

    private bool? isActive;

    private bool includeDeleted;

    [Inject]
    public required ProductAccessor ProductAccessor { get; set; }

    [Inject]
    public required CategoryAccessor CategoryAccessor { get; set; }

    [Inject]
    public required TaxRateAccessor TaxRateAccessor { get; set; }

    protected override Task OnInitializedAsync() =>
        LoadAsync(async () =>
        {
            categories = await CategoryAccessor.QueryListAsync(null, false, "SortOrder, Code", ApiHelper.MaxPageSize, 0, CancellationToken);
            categories = CategoryOrder.Sort(categories);
            categoryMap = categories.ToDictionary(static x => x.Id);
            taxRates = (await TaxRateAccessor.QueryListAsync(null, true, CancellationToken)).ToDictionary(static x => x.Id);
        });

    private string CategoryName(Guid id) => categoryMap.TryGetValue(id, out var category) ? category.Name : "-";

    private string TaxRateName(Guid id) => taxRates.TryGetValue(id, out var taxRate) ? taxRate.Name : "-";

    //--------------------------------------------------------------------------------
    // Grid
    //--------------------------------------------------------------------------------

    private async Task<GridData<ProductEntity>> LoadServerData(GridState<ProductEntity> state, CancellationToken cancellationToken)
    {
        var sort = state.SortDefinitions.FirstOrDefault();
        var order = SqlHelper.NormalizeSort(SortColumns, "Code", sort?.SortBy, sort?.Descending ?? false);
        var pattern = ApiHelper.ToLikePattern(Dialect, keyword);
        var total = await ProductAccessor.CountAsync(categoryId, pattern, isActive, null, includeDeleted, cancellationToken);
        var items = await ProductAccessor.QueryListAsync(categoryId, pattern, isActive, null, includeDeleted, order, state.PageSize, state.Page * state.PageSize, cancellationToken);
        return new GridData<ProductEntity> { TotalItems = (int)total, Items = items };
    }

    private Task SearchAsync() => Grid.ReloadServerData();

    private Task OnSearchKeyDown(KeyboardEventArgs args) =>
        args.Key == "Enter" ? SearchAsync() : Task.CompletedTask;

    //--------------------------------------------------------------------------------
    // Operation
    //--------------------------------------------------------------------------------

    private async Task AddAsync()
    {
        var form = await ShowEditDialogAsync<ProductEditDialog, ProductForm>("商品追加", new ProductForm { TaxRateId = taxRates.Values.FirstOrDefault(static x => x.IsDefault)?.Id });
        if (form is null)
        {
            return;
        }

        await RunAsync(async () =>
        {
            var now = UtcNow;
            var entity = FormMapper.ToProductEntity(form);
            entity.Id = Guid.CreateVersion7();
            entity.CreatedAt = now;
            entity.UpdatedAt = now;
            entity.Version = 1;
            await ProductAccessor.InsertAsync(entity, CancellationToken);
            Snackbar.AddSuccess("追加しました。");
        }, SearchAsync);
    }

    private async Task EditAsync(ProductEntity entity)
    {
        var form = await ShowEditDialogAsync<ProductEditDialog, ProductForm>("商品編集", FormMapper.ToProductForm(entity));
        if (form is null)
        {
            return;
        }

        await RunAsync(async () =>
        {
            var rows = await ProductAccessor.UpdateAsync(
                form.Id, form.Code, form.Barcode, form.Name, form.Kana, form.Brand, form.ModelNo, form.CategoryId!.Value, form.Kind, form.Price, form.TaxIncluded, form.TaxRateId!.Value,
                form.Cost, form.PointRate, form.RequiresSerial, form.TrackInventory, form.AllowsPriceOverride, form.Unit, form.IsActive, UtcNow, form.Version, CancellationToken);
            if (rows > 0)
            {
                Snackbar.AddSuccess("更新しました。");
            }
            else
            {
                NotifyVersionMismatch();
            }
        }, SearchAsync);
    }

    private async Task DeleteAsync(ProductEntity entity)
    {
        if (!await ConfirmDeleteAsync(entity.Name))
        {
            return;
        }

        await RunAsync(async () =>
        {
            if (await ProductAccessor.DeleteAsync(entity.Id, UtcNow, CancellationToken) > 0)
            {
                Snackbar.AddSuccess("削除しました。");
            }
            else
            {
                NotifyNotFound();
            }
        }, SearchAsync);
    }
}
