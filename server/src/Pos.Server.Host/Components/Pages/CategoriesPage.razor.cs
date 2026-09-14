namespace Pos.Server.Host.Components.Pages;

using Microsoft.AspNetCore.Components;

using MudBlazor;

using Pos.Server.Host.Components.Dialogs;
using Pos.Server.Host.Models.Forms;
using Pos.Server.Models.Entity;
using Pos.Server.Services;

// S-53 部門 (2 階層のツリー)
public sealed partial class CategoriesPage
{
    private List<CategoryEntity> items = [];
    private List<TreeItemData<CategoryEntity>> tree = [];
    private Dictionary<Guid, long> productCounts = [];

    [Inject]
    public required CategoryService CategoryService { get; set; }

    protected override Task OnInitializedAsync() => LoadAsync();

    private Task LoadAsync() =>
        LoadAsync(async () =>
        {
            items = await CategoryService.QueryAllAsync(false, CancellationToken);
            productCounts = await CategoryService.QueryProductCountsAsync(CancellationToken);
            tree = items
                .Where(static x => x.ParentId is null)
                .Select(parent => new TreeItemData<CategoryEntity>
                {
                    Value = parent,
                    Text = parent.Name,
                    Expanded = true,
                    Children = items.Where(x => x.ParentId == parent.Id).Select(static child => new TreeItemData<CategoryEntity> { Value = child, Text = child.Name }).ToList()
                })
                .ToList();
        });

    private long ProductCount(Guid id) => productCounts.GetValueOrDefault(id);

    private IReadOnlyList<CategoryEntity> Parents => items.Where(static x => x.ParentId is null).ToList();

    private async Task AddAsync(Guid? parentId)
    {
        var form = await ShowEditDialogAsync<CategoryEditDialog, CategoryForm>("部門追加", new CategoryForm { ParentId = parentId }, Styles.SmallDialog, p => p.Add(nameof(CategoryEditDialog.Parents), Parents));
        if (form is null)
        {
            return;
        }

        await RunAsync(async () => NotifyResult(await CategoryService.InsertAsync(CategoryForm.ToEntity(form), CancellationToken), "追加しました。"), LoadAsync);
    }

    private async Task EditAsync(CategoryEntity entity)
    {
        var form = await ShowEditDialogAsync<CategoryEditDialog, CategoryForm>("部門編集", CategoryForm.ToForm(entity), Styles.SmallDialog, p => p.Add(nameof(CategoryEditDialog.Parents), Parents));
        if (form is null)
        {
            return;
        }

        await RunAsync(async () => NotifyResult(await CategoryService.UpdateAsync(CategoryForm.ToEntity(form), CancellationToken), "更新しました。", invalid: "親部門に自分自身は指定できません。"), LoadAsync);
    }

    // 商品または子部門がある部門は削除できない
    private async Task DeleteAsync(CategoryEntity entity)
    {
        if (!await ConfirmDeleteAsync(entity.Name))
        {
            return;
        }

        await RunAsync(async () => NotifyResult(await CategoryService.DeleteAsync(entity.Id, CancellationToken), "削除しました。", inUse: "商品または子部門がある部門は削除できません。"), LoadAsync);
    }
}
