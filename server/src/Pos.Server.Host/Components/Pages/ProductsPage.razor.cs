namespace Pos.Server.Host.Components.Pages;

using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

using MudBlazor;

using Pos.Server.Host.Components.Dialogs;
using Pos.Server.Host.Helpers;
using Pos.Server.Host.Models.Forms;
using Pos.Server.Models.Entity;
using Pos.Server.Models.Parameters;
using Pos.Server.Services;

// 商品一覧 / 商品編集
public sealed partial class ProductsPage
{
    private const string DuplicateMessage = "商品コードまたはバーコードが重複しています。";

    private MudDataGrid<ProductEntity> Grid { get; set; } = default!;

    private List<CategoryEntity> categories = [];
    private Dictionary<Guid, CategoryEntity> categoryMap = [];
    private Dictionary<Guid, TaxRateEntity> taxRates = [];
    private Guid? categoryId;
    private string? keyword;
    private bool? isActive;
    private bool includeDeleted;

    [Inject]
    public required ProductService ProductService { get; set; }

    [Inject]
    public required CategoryService CategoryService { get; set; }

    [Inject]
    public required TaxRateService TaxRateService { get; set; }

    protected override Task OnInitializedAsync() =>
        LoadAsync(async () =>
        {
            categories = await CategoryService.QueryAllAsync(false, CancellationToken);
            categoryMap = categories.ToDictionary(static x => x.Id);
            taxRates = (await TaxRateService.QueryListAsync(null, true, CancellationToken)).ToDictionary(static x => x.Id);
        });

    private string CategoryName(Guid id) => categoryMap.TryGetValue(id, out var category) ? category.Name : "-";

    private string TaxRateName(Guid id) => taxRates.TryGetValue(id, out var taxRate) ? taxRate.Name : "-";

    //--------------------------------------------------------------------------------
    // Grid
    //--------------------------------------------------------------------------------

    private async Task<GridData<ProductEntity>> LoadServerData(GridState<ProductEntity> state, CancellationToken cancellationToken)
    {
        var sort = state.SortDefinitions.FirstOrDefault();
        var parameter = new ProductQueryParameter
        {
            CategoryId = categoryId,
            Keyword = keyword,
            IsActive = isActive,
            IncludeDeleted = includeDeleted,
            Sort = EnumHelper.Parse(sort?.SortBy, ProductSort.Code),
            Desc = sort?.Descending ?? false,
            Page = state.Page,
            Size = state.PageSize
        };
        var result = await ProductService.QueryPageAsync(parameter, cancellationToken);
        return new GridData<ProductEntity> { TotalItems = result.Total, Items = result.Items };
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

        await RunAsync(async () => NotifyResult(await ProductService.InsertAsync(ProductForm.ToEntity(form), CancellationToken), "追加しました。", duplicate: DuplicateMessage), SearchAsync);
    }

    private async Task EditAsync(ProductEntity entity)
    {
        var form = await ShowEditDialogAsync<ProductEditDialog, ProductForm>("商品編集", ProductForm.ToForm(entity));
        if (form is null)
        {
            return;
        }

        await RunAsync(async () => NotifyResult(await ProductService.UpdateAsync(ProductForm.ToEntity(form), CancellationToken), "更新しました。", duplicate: DuplicateMessage), SearchAsync);
    }

    private async Task DeleteAsync(ProductEntity entity)
    {
        if (!await ConfirmDeleteAsync(entity.Name))
        {
            return;
        }

        await RunAsync(async () => NotifyResult(await ProductService.DeleteAsync(entity.Id, CancellationToken), "削除しました。"), SearchAsync);
    }
}
