namespace Pos.Server.Host.Components.Pages;

using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

using MudBlazor;

using Pos.Server.Host.Components.Dialogs;
using Pos.Server.Host.Endpoints;
using Pos.Server.Host.Helpers;
using Pos.Server.Host.Models.Forms;
using Pos.Server.Models.Entity;
using Pos.Server.Models.Parameters;
using Pos.Server.Services;

// 商品一覧 / 商品編集 / CSV の取込
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

    // 一覧は率だけ (名称は編集で見る)
    private string TaxRateText(Guid id) => taxRates.TryGetValue(id, out var taxRate) ? taxRate.Rate.ToPercentText() : "-";

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

        await RunAsync(
            async () =>
            {
                var entity = ProductForm.ToEntity(form);
                var status = await ProductService.InsertAsync(entity, CancellationToken);
                NotifyResult(status, "追加しました。", duplicate: DuplicateMessage);
                if (status == DataWriteStatus.Success)
                {
                    await SaveImageAsync(entity.Id, form);
                }
            },
            SearchAsync);
    }

    private async Task EditAsync(ProductEntity entity)
    {
        var form = await ShowEditDialogAsync<ProductEditDialog, ProductForm>("商品編集", ProductForm.ToForm(entity), configure: p => p.Add(nameof(ProductEditDialog.CurrentImage), entity.ImageUrl));
        if (form is null)
        {
            return;
        }

        await RunAsync(
            async () =>
            {
                var result = await ProductService.UpdateAsync(ProductForm.ToEntity(form), CancellationToken);
                NotifyResult(result, "更新しました。", duplicate: DuplicateMessage);
                if (result.Status == DataWriteStatus.Success)
                {
                    await SaveImageAsync(entity.Id, form);
                }
            },
            SearchAsync);
    }

    // 画像は商品を保存できたときだけ反映する (選び直しがなければ何もしない)
    private async Task SaveImageAsync(Guid id, ProductForm form)
    {
        var result = form.NewImage is { } image
            ? await ProductService.SaveImageAsync(id, image, ApiRoutes.ProductImage(id), CancellationToken)
            : form.RemoveImage ? await ProductService.DeleteImageAsync(id, CancellationToken) : null;
        if ((result is not null) && (result.Status != DataWriteStatus.Success))
        {
            Snackbar.AddError("画像を保存できませんでした。");
        }
    }

    // 認証の導入時: 取込は Administrator に限る
    private async Task ImportAsync()
    {
        var reference = await DialogService.ShowAsync<ProductImportDialog>(string.Empty, Styles.LargeDialog);
        if (await reference.Result is { Canceled: false, Data: ProductImportResult result })
        {
            Snackbar.AddSuccess($"取り込みました (新規 {result.InsertCount} 件 / 更新 {result.UpdateCount} 件)。");
            await SearchAsync();
        }
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
